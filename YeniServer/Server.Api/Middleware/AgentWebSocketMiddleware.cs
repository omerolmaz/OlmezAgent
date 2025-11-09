using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Server.Application.Interfaces;
using Server.Application.DTOs.Device;
using Server.Domain.Enums;
using Server.Infrastructure.Data;
using Server.Api.Services;

namespace Server.Api.Middleware;

public class AgentWebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AgentWebSocketMiddleware> _logger;
    private readonly AgentConnectionManager _connectionManager;

    public AgentWebSocketMiddleware(
        RequestDelegate next, 
        ILogger<AgentWebSocketMiddleware> logger,
        AgentConnectionManager connectionManager)
    {
        _next = next;
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, IDeviceService deviceService)
    {
        if (context.Request.Path == "/agent.ashx" && context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation("New agent WebSocket connection from {RemoteIp}", context.Connection.RemoteIpAddress);
            
            await HandleAgentConnection(webSocket, context, dbContext, deviceService);
        }
        else
        {
            await _next(context);
        }
    }

    private async Task HandleAgentConnection(
        WebSocket webSocket, 
        HttpContext context, 
        ApplicationDbContext dbContext,
        IDeviceService deviceService)
    {
        var buffer = new byte[1024 * 16]; // 16KB buffer
        Guid? deviceId = null;
        var messageBuffer = new MemoryStream();
        
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (WebSocketException wsEx) when (wsEx.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogInformation("Client {DeviceId} closed connection prematurely", deviceId?.ToString() ?? "unknown");
                    break;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("WebSocket operation cancelled for device {DeviceId}", deviceId?.ToString() ?? "unknown");
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Client {DeviceId} initiated close handshake", deviceId?.ToString() ?? "unknown");
                    
                    if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Fragmented mesajları birleştir
                    messageBuffer.Write(buffer, 0, result.Count);
                    
                    // Tam mesaj gelene kadar bekle
                    if (!result.EndOfMessage)
                    {
                        _logger.LogDebug("Received fragment: {Size} bytes, waiting for more...", result.Count);
                        continue;
                    }
                    
                    var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.SetLength(0); // Buffer'ı temizle
                    
                    _logger.LogInformation("Received complete message: {Length} bytes", message.Length);
                    
                    try
                    {
                        var json = JsonDocument.Parse(message);
                        var root = json.RootElement;
                        
                        if (root.TryGetProperty("action", out var actionElement))
                        {
                            var action = actionElement.GetString();
                            
                            switch (action)
                            {
                                case "agenthello":
                                    await HandleAgentHello(root, webSocket, context);
                                    break;
                                    
                                case "agentinfo":
                                    var newDeviceId = await HandleAgentInfo(root, webSocket, deviceService, context);
                                    if (newDeviceId.HasValue)
                                    {
                                        deviceId = newDeviceId;
                                        _logger.LogInformation("Device ID set from agentinfo: {DeviceId}", deviceId);
                                    }
                                    break;
                                    
                                case "register":
                                    var regDeviceId = await HandleRegister(root, webSocket, deviceService);
                                    if (regDeviceId.HasValue)
                                    {
                                        deviceId = regDeviceId;
                                        _logger.LogInformation("Device ID set from register: {DeviceId}", deviceId);
                                    }
                                    break;
                                    
                                case "heartbeat":
                                    await HandleHeartbeat(deviceId, deviceService);
                                    break;
                                    
                                case "commandResult":
                                case "commandresult":
                                    await HandleCommandResult(root, dbContext, deviceService, context, webSocket);
                                    break;
                                    
                                default:
                                    _logger.LogWarning("Unknown action: {Action}", action);
                                    break;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse JSON message");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Binary protocol için - MeshCentral gibi
                    _logger.LogDebug("Received binary message: {Length} bytes", result.Count);
                    // TODO: Binary protokol implementasyonu
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInformation("WebSocket connection closed prematurely for device {DeviceId}", deviceId?.ToString() ?? "unknown");
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error for device {DeviceId}: {ErrorCode}", deviceId?.ToString() ?? "unknown", ex.WebSocketErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling agent connection for device {DeviceId}", deviceId?.ToString() ?? "unknown");
        }
        finally
        {
            messageBuffer?.Dispose();
            
            if (deviceId.HasValue)
            {
                // Connection manager'dan çıkar
                _connectionManager.RemoveConnection(deviceId.Value);
                
                // Device durumunu disconnected olarak güncelle
                try
                {
                    await deviceService.UpdateDeviceStatusAsync(new UpdateDeviceStatusRequest
                    {
                        DeviceId = deviceId.Value,
                        Status = ConnectionStatus.Disconnected
                    });
                    _logger.LogInformation("Device {DeviceId} disconnected and removed from connection manager", deviceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update device status for {DeviceId}", deviceId);
                }
            }
            
            // WebSocket'i güvenli şekilde kapat
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", CancellationToken.None);
                }
            }
            catch (WebSocketException)
            {
                // Zaten kapalı veya bağlantı kesilmiş - sessizce görmezden gel
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while closing WebSocket for device {DeviceId}", deviceId?.ToString() ?? "unknown");
            }
            finally
            {
                webSocket.Dispose();
            }
        }
    }

    private async Task<Guid?> HandleRegister(
        JsonElement root, 
        WebSocket webSocket, 
        IDeviceService deviceService)
    {
        try
        {
            var hostname = root.GetProperty("hostname").GetString() ?? "Unknown";
            var domain = root.TryGetProperty("domain", out var domainEl) ? domainEl.GetString() : null;
            var osVersion = root.TryGetProperty("osVersion", out var osEl) ? osEl.GetString() : null;
            var architecture = root.TryGetProperty("architecture", out var archEl) ? archEl.GetString() : null;
            var ipAddress = root.TryGetProperty("ipAddress", out var ipEl) ? ipEl.GetString() : null;
            var agentVersion = root.TryGetProperty("agentVersion", out var verEl) ? verEl.GetString() : null;

            var request = new RegisterDeviceRequest
            {
                Hostname = hostname ?? "Unknown",
                Domain = domain,
                OsVersion = osVersion,
                Architecture = architecture,
                IpAddress = ipAddress ?? "0.0.0.0",
                AgentVersion = agentVersion ?? "1.0.0"
            };

            var result = await deviceService.RegisterDeviceAsync(request);

            if (result.Success && result.Data != null)
            {
                // CRITICAL: Always add/update connection in manager
                _connectionManager.AddConnection(result.Data.Id, webSocket);
                _logger.LogInformation("Device added to connection manager: {DeviceId} ({Hostname})", 
                    result.Data.Id, hostname);
                
                var response = JsonSerializer.Serialize(new
                {
                    action = "registered",
                    deviceId = result.Data.Id,
                    status = "success"
                });

                var bytes = Encoding.UTF8.GetBytes(response);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None);

                _logger.LogInformation("Registration response sent to device: {DeviceId}", result.Data.Id);
                return result.Data.Id;
            }
            else
            {
                _logger.LogWarning("Device registration failed: {Error}", result.ErrorMessage);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device registration");
            return null;
        }
    }

    private async Task HandleHeartbeat(Guid? deviceId, IDeviceService deviceService)
    {
        if (deviceId.HasValue)
        {
            await deviceService.UpdateDeviceStatusAsync(new UpdateDeviceStatusRequest
            {
                DeviceId = deviceId.Value,
                Status = ConnectionStatus.Connected
            });
            _logger.LogDebug("Heartbeat from device {DeviceId}", deviceId);
        }
    }

    private async Task HandleAgentHello(JsonElement root, WebSocket webSocket, HttpContext context)
    {
        _logger.LogInformation("Agent hello received from {RemoteIp}", context.Connection.RemoteIpAddress);
        
        // MeshCentral protokolü: Server bilgilerini gönder
        var response = new
        {
            action = "serverhello",
            serverid = Environment.MachineName,
            version = "1.0.0",
            serverTime = DateTime.UtcNow,
            features = new[] { "agentinfo", "commands", "files" }
        };

        var responseJson = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(responseJson);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
        
        _logger.LogInformation("Sent serverhello response");
    }

    private async Task<Guid?> HandleAgentInfo(
        JsonElement root, 
        WebSocket webSocket, 
        IDeviceService deviceService,
        HttpContext context)
    {
        try
        {
            // MeshCentral protokolünden gelen bilgileri parse et
            var hostname = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : 
                          root.TryGetProperty("hostname", out var hostEl) ? hostEl.GetString() : "Unknown";
            
            var macAddress = root.TryGetProperty("macAddress", out var macEl) ? macEl.GetString() : null;
            var domain = root.TryGetProperty("domain", out var domainEl) ? domainEl.GetString() : null;
            var osVersion = root.TryGetProperty("osdesc", out var osEl) ? osEl.GetString() : null;
            var architecture = root.TryGetProperty("platform", out var platEl) ? platEl.GetString() : null;
            
            // IP adresini önce agent'ten al, yoksa connection'dan al
            var ipAddress = root.TryGetProperty("ipAddress", out var ipEl) ? ipEl.GetString() : null;
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "127.0.0.1" || ipAddress == "::1")
            {
                // Agent'ten gelen IP localhost ise connection IP'sini kullan
                var remoteIp = context.Connection.RemoteIpAddress;
                if (remoteIp != null)
                {
                    // IPv6 localhost'u IPv4'e çevir
                    if (remoteIp.ToString() == "::1")
                    {
                        ipAddress = "127.0.0.1";
                    }
                    else
                    {
                        ipAddress = remoteIp.ToString();
                    }
                }
            }
            
            var agentVersion = root.TryGetProperty("agentVersion", out var verEl) ? verEl.GetString() : 
                              root.TryGetProperty("ver", out var ver2El) ? ver2El.GetString() : null;

            _logger.LogInformation("Agent info - Hostname: {Hostname}, MAC: {MacAddress}, OS: {Os}, IP: {Ip}", 
                hostname, macAddress, osVersion, ipAddress);

            var request = new RegisterDeviceRequest
            {
                Hostname = hostname ?? "Unknown",
                MacAddress = macAddress,
                Domain = domain,
                OsVersion = osVersion,
                Architecture = architecture,
                IpAddress = ipAddress ?? "0.0.0.0",
                AgentVersion = agentVersion ?? "1.0.0"
            };

            var result = await deviceService.RegisterDeviceAsync(request);

            if (result.Success && result.Data != null)
            {
                // CRITICAL: Always add/update connection in manager (handles both new and existing devices)
                _connectionManager.AddConnection(result.Data.Id, webSocket);
                _logger.LogInformation("Device added to connection manager: {DeviceId} ({Hostname})", 
                    result.Data.Id, hostname);
                
                var response = JsonSerializer.Serialize(new
                {
                    action = "registered",
                    deviceId = result.Data.Id,
                    status = "success",
                    message = "Device registered successfully"
                });

                var bytes = Encoding.UTF8.GetBytes(response);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None);

                _logger.LogInformation("Registration response sent to device: {DeviceId}", result.Data.Id);
                
                // Request initial inventory refresh if we don't have recent inventory
                try
                {
                    var inventoryService = context.RequestServices.GetService(typeof(IInventoryService)) as IInventoryService;
                    if (inventoryService != null)
                    {
                        var existing = await inventoryService.GetInventoryAsync(result.Data.Id);
                        if (existing == null)
                        {
                            // Use seeded system admin user for system-initiated commands
                            var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
                            await inventoryService.RequestInventoryRefreshAsync(result.Data.Id, systemUserId);
                            _logger.LogInformation("Requested initial inventory refresh for device {DeviceId}", result.Data.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to request initial inventory refresh for device {DeviceId}", result.Data.Id);
                }

                return result.Data.Id;
            }
            else
            {
                _logger.LogWarning("Device registration failed: {Error}", result.ErrorMessage);
                
                // Hata mesajı gönder
                var errorResponse = JsonSerializer.Serialize(new
                {
                    action = "error",
                    message = result.ErrorMessage
                });
                
                var errorBytes = Encoding.UTF8.GetBytes(errorResponse);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(errorBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling agent info");
            return null;
        }
    }

    private async Task HandleCommandResult(JsonElement root, ApplicationDbContext dbContext, IDeviceService deviceService, HttpContext httpContext, WebSocket webSocket)
    {
        try
        {
            if (!root.TryGetProperty("commandId", out var commandIdProp))
            {
                _logger.LogWarning("CommandResult missing commandId");
                return;
            }

            var commandIdStr = commandIdProp.GetString();
            
            // If commandId is "system" or other non-GUID values, it's a system message (like registration agentinfo)
            // Check if it contains agentinfo in the result
            if (string.IsNullOrEmpty(commandIdStr) || !Guid.TryParse(commandIdStr, out var commandId))
            {
                _logger.LogInformation("System message received (commandId: {CommandId})", commandIdStr);
                
                // Check if this is an agentinfo system message for device registration
                if (root.TryGetProperty("result", out var resultEl) && resultEl.ValueKind == JsonValueKind.Object)
                {
                    if (resultEl.TryGetProperty("action", out var actionEl) && 
                        actionEl.GetString() == "agentinfo")
                    {
                        _logger.LogInformation("Processing agentinfo from system message");
                        await HandleAgentInfoFromResult(resultEl, deviceService, httpContext, webSocket);
                    }
                }
                return;
            }

            var command = await dbContext.Commands.FindAsync(commandId);
            if (command == null)
            {
                _logger.LogWarning("Command not found: {CommandId}", commandId);
                return;
            }

            // Get status
            var status = root.TryGetProperty("status", out var statusEl) 
                ? statusEl.GetString() ?? "Completed" 
                : "Completed";

            // Get result (can be string or JSON object)
            string? resultText = null;
            JsonElement resultEl2 = default;
            if (root.TryGetProperty("result", out resultEl2))
            {
                if (resultEl2.ValueKind == JsonValueKind.String)
                {
                    resultText = resultEl2.GetString();
                }
                else
                {
                    // Convert JSON object/array to string
                    resultText = JsonSerializer.Serialize(resultEl2);
                }
            }

            // Get success flag
            var success = root.TryGetProperty("success", out var successEl) && successEl.GetBoolean();

            // Get error message if any
            string? errorMessage = null;
            if (root.TryGetProperty("error", out var errorEl))
            {
                errorMessage = errorEl.GetString();
            }

            // Update command
            command.Status = success ? "Completed" : "Failed";
            command.Result = resultText;
            command.CompletedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(errorMessage))
            {
                command.ErrorMessage = errorMessage;
            }

            // Calculate execution duration if SentAt is set
            if (command.SentAt.HasValue)
            {
                command.ExecutionDurationMs = (long)(command.CompletedAt.Value - command.SentAt.Value).TotalMilliseconds;
            }

            await dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Command {CommandId} completed with status: {Status}, success: {Success}, duration: {Duration}ms, result: {Result}", 
                commandId, command.Status, success, command.ExecutionDurationMs, command.Result?.Substring(0, Math.Min(200, command.Result?.Length ?? 0)));

            // If this was an inventory response, attempt to persist inventory and lists
            try
            {
                if (string.Equals(command.CommandType, "getfullinventory", StringComparison.OrdinalIgnoreCase) && resultEl2.ValueKind == JsonValueKind.Object)
                {
                    var inventoryService = httpContext.RequestServices.GetService(typeof(IInventoryService)) as IInventoryService;
                    if (inventoryService != null)
                    {
                        // Parse basic inventory fields
                        var collectedAt = DateTime.UtcNow;
                        if (resultEl2.TryGetProperty("timestampUtc", out var tsEl) && tsEl.ValueKind == JsonValueKind.String)
                        {
                            if (DateTimeOffset.TryParse(tsEl.GetString(), out var dto))
                            {
                                collectedAt = dto.UtcDateTime;
                            }
                        }

                        var deviceInventory = new Server.Domain.Entities.DeviceInventory
                        {
                            DeviceId = command.DeviceId,
                            CollectedAt = collectedAt,
                            UpdatedAt = DateTime.UtcNow
                        };

                        // hardware object
                        if (resultEl2.TryGetProperty("hardware", out var hw) && hw.ValueKind == JsonValueKind.Object)
                        {
                            if (hw.TryGetProperty("manufacturer", out var man) && man.ValueKind == JsonValueKind.String)
                                deviceInventory.Manufacturer = man.GetString();
                            if (hw.TryGetProperty("model", out var mod) && mod.ValueKind == JsonValueKind.String)
                                deviceInventory.Model = mod.GetString();
                            if (hw.TryGetProperty("totalPhysicalMemory", out var mem) && (mem.ValueKind == JsonValueKind.Number || mem.ValueKind == JsonValueKind.String))
                            {
                                if (mem.ValueKind == JsonValueKind.Number && mem.TryGetInt64(out var m1)) deviceInventory.TotalPhysicalMemory = m1;
                                else if (mem.ValueKind == JsonValueKind.String && long.TryParse(mem.GetString(), out var m2)) deviceInventory.TotalPhysicalMemory = m2;
                            }
                            if (hw.TryGetProperty("osVersion", out var osv) && osv.ValueKind == JsonValueKind.String)
                                deviceInventory.OsVersion = osv.GetString();
                            if (hw.TryGetProperty("osArchitecture", out var osarch) && osarch.ValueKind == JsonValueKind.String)
                                deviceInventory.OsArchitecture = osarch.GetString();
                            if (hw.TryGetProperty("osName", out var osNameEl) && osNameEl.ValueKind == JsonValueKind.String)
                                deviceInventory.OsName = osNameEl.GetString();
                            if (hw.TryGetProperty("osBuild", out var osBuildEl) && osBuildEl.ValueKind == JsonValueKind.String)
                                deviceInventory.OsBuild = osBuildEl.GetString();
                            if (hw.TryGetProperty("osInstallDate", out var osInstallEl) && osInstallEl.ValueKind == JsonValueKind.String)
                            {
                                if (DateTime.TryParse(osInstallEl.GetString(), out var osInstallDate))
                                {
                                    deviceInventory.OsInstallDate = osInstallDate;
                                }
                            }
                            if (hw.TryGetProperty("osSerialNumber", out var osSerialEl) && osSerialEl.ValueKind == JsonValueKind.String)
                                deviceInventory.OsSerialNumber = osSerialEl.GetString();
                            if (hw.TryGetProperty("serialNumber", out var serialEl) && serialEl.ValueKind == JsonValueKind.String)
                                deviceInventory.SerialNumber = serialEl.GetString();
                            if (hw.TryGetProperty("systemFamily", out var systemFamilyEl) && systemFamilyEl.ValueKind == JsonValueKind.String)
                                deviceInventory.SystemFamily = systemFamilyEl.GetString();
                            if (hw.TryGetProperty("systemSKU", out var systemSkuEl) && systemSkuEl.ValueKind == JsonValueKind.String)
                                deviceInventory.SystemSKU = systemSkuEl.GetString();
                            if (hw.TryGetProperty("biosManufacturer", out var biosManEl) && biosManEl.ValueKind == JsonValueKind.String)
                                deviceInventory.BiosManufacturer = biosManEl.GetString();
                            if (hw.TryGetProperty("biosVersion", out var biosVerEl) && biosVerEl.ValueKind == JsonValueKind.String)
                                deviceInventory.BiosVersion = biosVerEl.GetString();
                            if (hw.TryGetProperty("biosReleaseDate", out var biosDateEl) && biosDateEl.ValueKind == JsonValueKind.String)
                            {
                                if (DateTime.TryParse(biosDateEl.GetString(), out var biosReleaseDate))
                                {
                                    deviceInventory.BiosReleaseDate = biosReleaseDate;
                                }
                            }
                            if (hw.TryGetProperty("processorName", out var procNameEl) && procNameEl.ValueKind == JsonValueKind.String)
                                deviceInventory.ProcessorName = procNameEl.GetString();
                            if (hw.TryGetProperty("processorManufacturer", out var procManEl) && procManEl.ValueKind == JsonValueKind.String)
                                deviceInventory.ProcessorManufacturer = procManEl.GetString();
                            if (hw.TryGetProperty("processorArchitecture", out var procArchEl) && procArchEl.ValueKind == JsonValueKind.String)
                                deviceInventory.ProcessorArchitecture = procArchEl.GetString();
                            if (hw.TryGetProperty("processorCores", out var procCoresEl))
                            {
                                if (procCoresEl.ValueKind == JsonValueKind.Number && procCoresEl.TryGetInt32(out var cores))
                                    deviceInventory.ProcessorCores = cores;
                                else if (procCoresEl.ValueKind == JsonValueKind.String && int.TryParse(procCoresEl.GetString(), out var coresStr))
                                    deviceInventory.ProcessorCores = coresStr;
                            }
                            if (hw.TryGetProperty("processorLogicalProcessors", out var procLogicalEl))
                            {
                                if (procLogicalEl.ValueKind == JsonValueKind.Number && procLogicalEl.TryGetInt32(out var logical))
                                    deviceInventory.ProcessorLogicalProcessors = logical;
                                else if (procLogicalEl.ValueKind == JsonValueKind.String && int.TryParse(procLogicalEl.GetString(), out var logicalStr))
                                    deviceInventory.ProcessorLogicalProcessors = logicalStr;
                            }
                            if (hw.TryGetProperty("processorMaxClockSpeed", out var procClockEl))
                            {
                                if (procClockEl.ValueKind == JsonValueKind.Number && procClockEl.TryGetInt32(out var clock))
                                    deviceInventory.ProcessorMaxClockSpeed = clock;
                                else if (procClockEl.ValueKind == JsonValueKind.String && int.TryParse(procClockEl.GetString(), out var clockStr))
                                    deviceInventory.ProcessorMaxClockSpeed = clockStr;
                            }
                            if (hw.TryGetProperty("memorySlots", out var memorySlotsEl))
                            {
                                if (memorySlotsEl.ValueKind == JsonValueKind.Number && memorySlotsEl.TryGetInt32(out var slots))
                                    deviceInventory.MemorySlots = slots;
                                else if (memorySlotsEl.ValueKind == JsonValueKind.String && int.TryParse(memorySlotsEl.GetString(), out var slotsStr))
                                    deviceInventory.MemorySlots = slotsStr;
                            }
                            if (hw.TryGetProperty("domain", out var domainEl) && domainEl.ValueKind == JsonValueKind.String)
                                deviceInventory.DomainName = domainEl.GetString();
                        }

                        // network/interfaces - store as JSON string for now
                        if (resultEl2.TryGetProperty("interfaces", out var ifs) && (ifs.ValueKind == JsonValueKind.Array || ifs.ValueKind == JsonValueKind.Object))
                        {
                            deviceInventory.NetworkAdapters = JsonSerializer.Serialize(ifs);
                        }

                        // disks -> store as JSON string
                        if (resultEl2.TryGetProperty("disks", out var disks) && (disks.ValueKind == JsonValueKind.Array || disks.ValueKind == JsonValueKind.Object))
                        {
                            deviceInventory.DiskDrives = JsonSerializer.Serialize(disks);
                        }

                        // host-level simple fields
                        if (resultEl2.TryGetProperty("hostname", out var hn) && hn.ValueKind == JsonValueKind.String)
                            deviceInventory.HostName = hn.GetString();

                        if (resultEl2.TryGetProperty("primaryIpAddress", out var pip) && pip.ValueKind == JsonValueKind.String)
                            deviceInventory.PrimaryIpAddress = pip.GetString();

                        if (resultEl2.TryGetProperty("primaryMacAddress", out var pmac) && pmac.ValueKind == JsonValueKind.String)
                            deviceInventory.PrimaryMacAddress = pmac.GetString();
                        if (resultEl2.TryGetProperty("domainName", out var domainNameEl) && domainNameEl.ValueKind == JsonValueKind.String)
                            deviceInventory.DomainName = domainNameEl.GetString();
                        if (resultEl2.TryGetProperty("totalDiskSpace", out var totalDiskEl))
                        {
                            if (totalDiskEl.ValueKind == JsonValueKind.Number && totalDiskEl.TryGetInt64(out var totalDisk))
                                deviceInventory.TotalDiskSpace = totalDisk;
                            else if (totalDiskEl.ValueKind == JsonValueKind.String && long.TryParse(totalDiskEl.GetString(), out var totalDiskStr))
                                deviceInventory.TotalDiskSpace = totalDiskStr;
                        }
                        if (resultEl2.TryGetProperty("diskCount", out var diskCountEl))
                        {
                            if (diskCountEl.ValueKind == JsonValueKind.Number && diskCountEl.TryGetInt32(out var diskCount))
                                deviceInventory.DiskCount = diskCount;
                            else if (diskCountEl.ValueKind == JsonValueKind.String && int.TryParse(diskCountEl.GetString(), out var diskCountStr))
                                deviceInventory.DiskCount = diskCountStr;
                        }
                        if (resultEl2.TryGetProperty("graphicsCard", out var graphicsCardEl) && graphicsCardEl.ValueKind == JsonValueKind.String)
                            deviceInventory.GraphicsCard = graphicsCardEl.GetString();
                        if (resultEl2.TryGetProperty("graphicsCardMemory", out var graphicsMemEl) && graphicsMemEl.ValueKind == JsonValueKind.String)
                            deviceInventory.GraphicsCardMemory = graphicsMemEl.GetString();
                        if (resultEl2.TryGetProperty("currentResolution", out var resolutionEl) && resolutionEl.ValueKind == JsonValueKind.String)
                            deviceInventory.CurrentResolution = resolutionEl.GetString();

                        // software list
                        var softwareList = new List<Server.Domain.Entities.InstalledSoftware>();
                        if (resultEl2.TryGetProperty("software", out var sw) && sw.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in sw.EnumerateArray())
                            {
                                try
                                {
                                    var name = el.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() ?? string.Empty : string.Empty;
                                    if (string.IsNullOrEmpty(name)) continue;

                                    DateTime? installDate = null;
                                    if (el.TryGetProperty("installDate", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                                    {
                                        if (DateTime.TryParse(idEl.GetString(), out var dt)) installDate = dt;
                                    }

                                    var soft = new Server.Domain.Entities.InstalledSoftware
                                    {
                                        Id = Guid.NewGuid(),
                                        DeviceId = command.DeviceId,
                                        CollectedAt = collectedAt,
                                        Name = name,
                                        Version = el.TryGetProperty("version", out var verEl) && verEl.ValueKind == JsonValueKind.String ? verEl.GetString() : null,
                                        Publisher = el.TryGetProperty("publisher", out var pubEl) && pubEl.ValueKind == JsonValueKind.String ? pubEl.GetString() : null,
                                        InstallDate = installDate,
                                        UninstallString = el.TryGetProperty("uninstallString", out var usEl) && usEl.ValueKind == JsonValueKind.String ? usEl.GetString() : null,
                                        InstallLocation = el.TryGetProperty("installLocation", out var ilEl) && ilEl.ValueKind == JsonValueKind.String ? ilEl.GetString() : null,
                                        RegistryPath = el.TryGetProperty("registryPath", out var rpEl) && rpEl.ValueKind == JsonValueKind.String ? rpEl.GetString() : null
                                    };
                                    softwareList.Add(soft);
                                }
                                catch { /* ignore bad item */ }
                            }
                        }

                        // patches list
                        var patchList = new List<Server.Domain.Entities.InstalledPatch>();
                        if (resultEl2.TryGetProperty("patches", out var pch) && pch.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in pch.EnumerateArray())
                            {
                                try
                                {
                                    var hotfix = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
                                    if (string.IsNullOrEmpty(hotfix)) continue;

                                    DateTime? installedOn = null;
                                    if (el.TryGetProperty("installedOn", out var iOn) && iOn.ValueKind == JsonValueKind.String)
                                    {
                                        if (DateTime.TryParse(iOn.GetString(), out var dt)) installedOn = dt;
                                    }

                                    var patch = new Server.Domain.Entities.InstalledPatch
                                    {
                                        Id = Guid.NewGuid(),
                                        DeviceId = command.DeviceId,
                                        CollectedAt = collectedAt,
                                        HotFixId = hotfix ?? string.Empty,
                                        Description = el.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String ? descEl.GetString() : null,
                                        InstalledOn = installedOn
                                    };
                                    patchList.Add(patch);
                                }
                                catch { /* ignore bad item */ }
                            }
                        }

                        // Persist
                        await inventoryService.SaveInventoryAsync(deviceInventory);
                        if (softwareList.Count > 0) await inventoryService.SaveInstalledSoftwareAsync(command.DeviceId, softwareList);
                        if (patchList.Count > 0) await inventoryService.SaveInstalledPatchesAsync(command.DeviceId, patchList);

                        _logger.LogInformation("Persisted inventory for device {DeviceId}: software={SoftwareCount}, patches={PatchCount}", command.DeviceId, softwareList.Count, patchList.Count);
                    }
                }
            }
            catch (Exception invEx)
            {
                _logger.LogError(invEx, "Failed to persist inventory for command {CommandId}", commandId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command result");
        }
    }
    
    private async Task<Guid?> HandleAgentInfoFromResult(
        JsonElement resultElement,
        IDeviceService deviceService,
        HttpContext context,
        WebSocket webSocket)
    {
        try
        {
            var hostname = resultElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Unknown";
            var macAddress = resultElement.TryGetProperty("macAddress", out var macEl) ? macEl.GetString() : null;
            var domain = resultElement.TryGetProperty("domain", out var domainEl) ? domainEl.GetString() : null;
            var osVersion = resultElement.TryGetProperty("osdesc", out var osEl) ? osEl.GetString() : null;
            var architecture = resultElement.TryGetProperty("platform", out var platEl) ? platEl.GetString() : null;
            var ipAddress = resultElement.TryGetProperty("ipAddress", out var ipEl) ? ipEl.GetString() : null;
            var agentVersion = resultElement.TryGetProperty("agentVersion", out var verEl) ? verEl.GetString() : 
                              resultElement.TryGetProperty("ver", out var ver2El) ? ver2El.GetString() : null;

            _logger.LogInformation("Agent info from result - Hostname: {Hostname}, MAC: {MacAddress}, OS: {Os}, IP: {Ip}", 
                hostname, macAddress, osVersion, ipAddress);

            var request = new RegisterDeviceRequest
            {
                Hostname = hostname ?? "Unknown",
                MacAddress = macAddress,
                Domain = domain,
                OsVersion = osVersion,
                Architecture = architecture,
                IpAddress = ipAddress ?? "0.0.0.0",
                AgentVersion = agentVersion ?? "1.0.0"
            };

            var result = await deviceService.RegisterDeviceAsync(request);

            if (result.Success && result.Data != null)
            {
                // CRITICAL: Add connection to manager
                _connectionManager.AddConnection(result.Data.Id, webSocket);
                _logger.LogInformation("Device registered via system message: {DeviceId} ({Hostname}, MAC: {MacAddress})", 
                    result.Data.Id, hostname, macAddress);
                return result.Data.Id;
            }
            else
            {
                _logger.LogWarning("Device registration failed: {Error}", result.ErrorMessage);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling agentinfo from result");
            return null;
        }
    }
}
