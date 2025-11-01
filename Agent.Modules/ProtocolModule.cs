using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

/// <summary>
/// Handles MeshCentral-style protocol messages (serverhello, registered, etc.)
/// </summary>
public sealed class ProtocolModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "serverhello",
        "registered",
        "error"
    };

    public ProtocolModule(ILogger<ProtocolModule> logger) : base(logger)
    {
    }

    public override string Name => "ProtocolModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        var action = command.Action.ToLowerInvariant();
        
        switch (action)
        {
            case "serverhello":
                await HandleServerHello(command, context).ConfigureAwait(false);
                return true;
                
            case "registered":
                await HandleRegistered(command, context).ConfigureAwait(false);
                return true;
                
            case "error":
                HandleError(command, context);
                return true;
                
            default:
                return false;
        }
    }
    
    private async Task HandleServerHello(AgentCommand command, AgentContext context)
    {
        Logger.LogInformation("Received serverhello from server");
        
        // Extract server info from payload if available
        try
        {
            if (command.Payload.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var hasServerId = command.Payload.TryGetProperty("serverid", out var serverIdProp);
                var hasVersion = command.Payload.TryGetProperty("version", out var versionProp);
                var hasTime = command.Payload.TryGetProperty("serverTime", out var timeProp);
                
                if (hasServerId || hasVersion || hasTime)
                {
                    var serverId = hasServerId ? serverIdProp.GetString() : "unknown";
                    var serverVersion = hasVersion ? versionProp.GetString() : "unknown";
                    var serverTime = hasTime ? timeProp.GetString() : "unknown";
                    
                    Logger.LogInformation("Server ID: {ServerId}, Version: {Version}, Time: {Time}", 
                        serverId, serverVersion, serverTime);
                }
                else
                {
                    Logger.LogDebug("Serverhello received without server metadata");
                }
            }
            
            // Update connection status
            context.ConnectionStatus = ConnectionStatus.Connected;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Could not parse serverhello payload - continuing with registration");
        }
        
        // Send agentinfo to complete registration
        await SendAgentInfo(context, command.CancellationToken).ConfigureAwait(false);
    }
    
    private async Task SendAgentInfo(AgentContext context, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Sending agentinfo for registration");
        
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version?.ToString() ?? "1.0.0";
        
        var agentInfo = new JsonObject
        {
            ["action"] = "agentinfo",
            ["name"] = Environment.MachineName,
            ["osdesc"] = Environment.OSVersion.VersionString,
            ["platform"] = GetPlatformString(),
            ["domain"] = Environment.UserDomainName,
            ["ver"] = version,
            ["username"] = Environment.UserName,
            ["ipAddress"] = GetLocalIPAddress(),
            ["macAddress"] = GetMacAddress(),
            ["agentVersion"] = version,
            ["agentName"] = "olmez Agent",
            ["processorCount"] = Environment.ProcessorCount,
            ["is64Bit"] = Environment.Is64BitOperatingSystem,
            ["architecture"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        
        var deviceId = context.Options.DeviceId ?? Environment.MachineName;
        agentInfo["deviceId"] = deviceId;
        
        if (!string.IsNullOrWhiteSpace(context.Options.EnrollmentKey))
        {
            agentInfo["enrollmentKey"] = context.Options.EnrollmentKey;
        }
        
        var result = new CommandResult("agentinfo", "system", null, null, agentInfo);
        await context.ResponseWriter.SendAsync(result, cancellationToken).ConfigureAwait(false);
        
        Logger.LogInformation("Agentinfo sent");
    }
    
    private Task HandleRegistered(AgentCommand command, AgentContext context)
    {
        Logger.LogInformation("Device registered successfully");
        
        try
        {
            if (command.Payload.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var hasDeviceId = command.Payload.TryGetProperty("deviceId", out var deviceIdProp);
                var hasStatus = command.Payload.TryGetProperty("status", out var statusProp);
                var hasMessage = command.Payload.TryGetProperty("message", out var messageProp);
                
                var deviceId = hasDeviceId ? deviceIdProp.GetString() : "unknown";
                var status = hasStatus ? statusProp.GetString() : "registered";
                var message = hasMessage ? messageProp.GetString() : "Device registered";
                
                Logger.LogInformation("Registration - DeviceId: {DeviceId}, Status: {Status}, Message: {Message}",
                    deviceId, status, message);
            }
            
            // Update connection status
            context.ConnectionStatus = ConnectionStatus.Connected;
            
            // Note: Cannot update DeviceId as it's init-only property
            // It should be set from configuration or environment
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Could not parse registered payload - continuing anyway");
        }
        
        return Task.CompletedTask;
    }
    
    private void HandleError(AgentCommand command, AgentContext context)
    {
        try
        {
            var message = "Unknown error";
            
            if (command.Payload.ValueKind == System.Text.Json.JsonValueKind.Object &&
                command.Payload.TryGetProperty("message", out var messageProp))
            {
                message = messageProp.GetString() ?? "Unknown error";
            }
            
            Logger.LogError("Server error: {Message}", message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Server error: Could not parse error message");
        }
    }
    
    private static string GetPlatformString()
    {
        if (OperatingSystem.IsWindows()) return "win";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS()) return "macos";
        return "unknown";
    }
    
    private static string GetLocalIPAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
            // Ignore
        }
        return "127.0.0.1";
    }
    
    private static string GetMacAddress()
    {
        try
        {
            var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                    nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                {
                    var address = nic.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(address))
                    {
                        return address;
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        return "00-00-00-00-00-00";
    }
}
