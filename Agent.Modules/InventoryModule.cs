using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Agent.Modules;

public sealed class InventoryModule : AgentModuleBase
{
    private static readonly IReadOnlyCollection<string> Actions = new[]
    {
        "getfullinventory",
        "getinstalledsoftware",
        "getinstalledpatches",
        "getpendingupdates",
        "sysinfo",
        "cpuinfo",
        "netinfo"
    };

    public InventoryModule(ILogger<InventoryModule> logger) : base(logger)
    {
    }

    public override string Name => "InventoryModule";

    public override IReadOnlyCollection<string> SupportedActions => Actions;

    public override async Task<bool> HandleAsync(AgentCommand command, AgentContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            await SendNotImplementedAsync(command, context, "Inventory actions are currently supported on Windows only.")
                .ConfigureAwait(false);
            return true;
        }

        switch (command.Action.ToLowerInvariant())
        {
            case "getinstalledsoftware":
                await HandleInstalledSoftwareAsync(command, context).ConfigureAwait(false);
                return true;
            case "getinstalledpatches":
                await HandleInstalledPatchesAsync(command, context).ConfigureAwait(false);
                return true;
            case "getpendingupdates":
                await HandlePendingUpdatesAsync(command, context).ConfigureAwait(false);
                return true;
            case "getfullinventory":
                await HandleFullInventoryAsync(command, context).ConfigureAwait(false);
                return true;
            case "sysinfo":
                await HandleSysInfoAsync(command, context).ConfigureAwait(false);
                return true;
            case "cpuinfo":
                await HandleCpuInfoAsync(command, context).ConfigureAwait(false);
                return true;
            case "netinfo":
                await HandleNetInfoAsync(command, context).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleInstalledSoftwareAsync(AgentCommand command, AgentContext context)
    {
        var software = await Task.Run(GetInstalledSoftware).ConfigureAwait(false);
        var payload = new JsonObject
        {
            ["software"] = software
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandleInstalledPatchesAsync(AgentCommand command, AgentContext context)
    {
        var patches = await Task.Run(GetInstalledPatches).ConfigureAwait(false);
        var payload = new JsonObject
        {
            ["patches"] = patches
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandlePendingUpdatesAsync(AgentCommand command, AgentContext context)
    {
        var updates = await Task.Run(GetPendingUpdates).ConfigureAwait(false);
        var payload = new JsonObject
        {
            ["updates"] = updates
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandleFullInventoryAsync(AgentCommand command, AgentContext context)
    {
        var inventory = new JsonObject
        {
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };

        var softwareTask = Task.Run(GetInstalledSoftware);
        var patchesTask = Task.Run(GetInstalledPatches);
        var updatesTask = Task.Run(GetPendingUpdates);
        var hardwareTask = Task.Run(GetHardwareInfo);
        var disksTask = Task.Run(GetDiskInfo);
        var servicesTask = Task.Run(GetServices);

        await Task.WhenAll(softwareTask, patchesTask, updatesTask, hardwareTask, disksTask, servicesTask)
            .ConfigureAwait(false);

        inventory["software"] = softwareTask.Result;
        inventory["patches"] = patchesTask.Result;
        inventory["pendingUpdates"] = updatesTask.Result;
        inventory["hardware"] = hardwareTask.Result;
        inventory["disks"] = disksTask.Result;
        inventory["services"] = servicesTask.Result;

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            inventory)).ConfigureAwait(false);
    }

    private async Task HandleSysInfoAsync(AgentCommand command, AgentContext context)
    {
        var sysInfo = await Task.Run(GetHardwareInfo).ConfigureAwait(false);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            sysInfo)).ConfigureAwait(false);
    }

    private async Task HandleCpuInfoAsync(AgentCommand command, AgentContext context)
    {
        var array = await Task.Run(GetCpuInfo).ConfigureAwait(false);
        var payload = new JsonObject
        {
            ["processors"] = array
        };
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private async Task HandleNetInfoAsync(AgentCommand command, AgentContext context)
    {
        var payload = new JsonObject
        {
            ["interfaces"] = await Task.Run(GetNetworkInterfaces).ConfigureAwait(false)
        };

        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            payload)).ConfigureAwait(false);
    }

    private static JsonArray GetInstalledSoftware()
    {
        var result = new JsonArray();
        try
        {
            var uninstallKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
            {
                foreach (var keyPath in uninstallKeys)
                {
                    using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
                    using var key = baseKey.OpenSubKey(keyPath);
                    if (key == null)
                    {
                        continue;
                    }
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var appKey = key.OpenSubKey(subKeyName);
                        if (appKey == null)
                        {
                            continue;
                        }

                        var displayName = appKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            continue;
                        }

                        var entry = new JsonObject
                        {
                            ["name"] = displayName,
                            ["version"] = appKey.GetValue("DisplayVersion") as string,
                            ["publisher"] = appKey.GetValue("Publisher") as string,
                            ["installDate"] = appKey.GetValue("InstallDate") as string,
                            ["uninstallString"] = appKey.GetValue("UninstallString") as string
                        };
                        result.Add(entry);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Add(new JsonObject
            {
                ["error"] = $"Software enumeration failed: {ex.Message}"
            });
        }

        return result;
    }

    private static JsonArray GetInstalledPatches()
    {
        var result = new JsonArray();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT HotFixID, Description, InstalledOn FROM Win32_QuickFixEngineering");
            foreach (ManagementObject patch in searcher.Get())
            {
                result.Add(new JsonObject
                {
                    ["id"] = patch["HotFixID"]?.ToString(),
                    ["description"] = patch["Description"]?.ToString(),
                    ["installedOn"] = patch["InstalledOn"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            result.Add(new JsonObject { ["error"] = $"Patch enumeration failed: {ex.Message}" });
        }

        return result;
    }

    private static JsonArray GetPendingUpdates()
    {
        var result = new JsonArray();
        try
        {
            var updateSessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (updateSessionType == null)
            {
                return result;
            }

            dynamic session = Activator.CreateInstance(updateSessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Software'");
            foreach (dynamic update in searchResult.Updates)
            {
                JsonArray kbArray;
                try
                {
                    var kbIds = (string[])update.KBArticleIDs;
                    kbArray = new JsonArray(kbIds.Select(id => JsonValue.Create(id)).ToArray());
                }
                catch
                {
                    kbArray = new JsonArray();
                }

                JsonArray categoryArray = new();
                try
                {
                    dynamic categories = update.Categories;
                    var count = (int)categories.Count;
                    for (var i = 0; i < count; i++)
                    {
                        dynamic category = categories.Item(i);
                        categoryArray.Add(JsonValue.Create(category.Name as string));
                    }
                }
                catch
                {
                    // ignore
                }

                long TryGetLong(dynamic value)
                {
                    try
                    {
                        return (long)value;
                    }
                    catch
                    {
                        return 0;
                    }
                }

                var updateJson = new JsonObject
                {
                    ["title"] = (string)update.Title,
                    ["description"] = (string)update.Description,
                    ["isDownloaded"] = (bool)update.IsDownloaded,
                    ["kbArticleIds"] = kbArray,
                    ["categories"] = categoryArray,
                    ["maxDownloadSize"] = JsonValue.Create(TryGetLong(update.MaxDownloadSize)),
                    ["minDownloadSize"] = JsonValue.Create(TryGetLong(update.MinDownloadSize))
                };
                result.Add(updateJson);
            }
        }
        catch (Exception ex)
        {
            result.Add(new JsonObject { ["error"] = $"Pending updates enumeration failed: {ex.Message}" });
        }

        return result;
    }

    private static JsonObject GetHardwareInfo()
    {
        var hardware = new JsonObject();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
            var info = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (info != null)
            {
                hardware["manufacturer"] = info["Manufacturer"]?.ToString();
                hardware["model"] = info["Model"]?.ToString();
                hardware["totalPhysicalMemory"] = info["TotalPhysicalMemory"]?.ToString();
            }

            hardware["osVersion"] = Environment.OSVersion.VersionString;
            hardware["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString();
        }
        catch (Exception ex)
        {
            hardware["error"] = $"Hardware query failed: {ex.Message}";
        }

        return hardware;
    }

    private static JsonArray GetCpuInfo()
    {
        var cpus = new JsonArray();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject cpu in searcher.Get())
            {
                cpus.Add(new JsonObject
                {
                    ["name"] = cpu["Name"]?.ToString(),
                    ["cores"] = JsonValue.Create(cpu["NumberOfCores"]?.ToString()),
                    ["logicalProcessors"] = JsonValue.Create(cpu["NumberOfLogicalProcessors"]?.ToString()),
                    ["maxClockSpeed"] = JsonValue.Create(cpu["MaxClockSpeed"]?.ToString())
                });
            }
        }
        catch (Exception ex)
        {
            cpus.Add(new JsonObject { ["error"] = $"CPU query failed: {ex.Message}" });
        }

        return cpus;
    }

    private static JsonArray GetDiskInfo()
    {
        var disks = new JsonArray();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, Size, FreeSpace, FileSystem FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (ManagementObject disk in searcher.Get())
            {
                disks.Add(new JsonObject
                {
                    ["deviceId"] = disk["DeviceID"]?.ToString(),
                    ["size"] = disk["Size"]?.ToString(),
                    ["freeSpace"] = disk["FreeSpace"]?.ToString(),
                    ["fileSystem"] = disk["FileSystem"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            disks.Add(new JsonObject { ["error"] = $"Disk query failed: {ex.Message}" });
        }

        return disks;
    }

    private static JsonArray GetServices()
    {
        var services = new JsonArray();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode FROM Win32_Service");
            foreach (ManagementObject service in searcher.Get())
            {
                services.Add(new JsonObject
                {
                    ["name"] = service["Name"]?.ToString(),
                    ["displayName"] = service["DisplayName"]?.ToString(),
                    ["state"] = service["State"]?.ToString(),
                    ["startMode"] = service["StartMode"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            services.Add(new JsonObject { ["error"] = $"Service query failed: {ex.Message}" });
        }

        return services;
    }

    private static JsonArray GetNetworkInterfaces()
    {
        var result = new JsonArray();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ipProps = nic.GetIPProperties();
                var ips = new JsonArray(ipProps.UnicastAddresses.Select(ip => JsonValue.Create(ip.Address.ToString())).ToArray());
                result.Add(new JsonObject
                {
                    ["name"] = nic.Name,
                    ["description"] = nic.Description,
                    ["status"] = nic.OperationalStatus.ToString(),
                    ["speed"] = JsonValue.Create(nic.Speed),
                    ["macAddress"] = nic.GetPhysicalAddress().ToString(),
                    ["ipAddresses"] = ips
                });
            }
        }
        catch (Exception ex)
        {
            result.Add(new JsonObject { ["error"] = $"Network interface query failed: {ex.Message}" });
        }

        return result;
    }
}
