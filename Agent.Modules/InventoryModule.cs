using Agent.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
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
        "netinfo",
        "smbios",
        "vm",
        "wifiscan",
        "perfcounters"
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
            case "smbios":
                await HandleSMBIOSAsync(command, context).ConfigureAwait(false);
                return true;
            case "vm":
                await HandleVMDetectionAsync(command, context).ConfigureAwait(false);
                return true;
            case "wifiscan":
                await HandleWiFiScanAsync(command, context).ConfigureAwait(false);
                return true;
            case "perfcounters":
                await HandlePerfCountersAsync(command, context).ConfigureAwait(false);
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

    private async Task HandleSMBIOSAsync(AgentCommand command, AgentContext context)
    {
        var smbios = await Task.Run(GetSMBIOSInfo).ConfigureAwait(false);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            smbios)).ConfigureAwait(false);
    }

    private static JsonObject GetSMBIOSInfo()
    {
        var info = new JsonObject();
        try
        {
            // BIOS bilgileri
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
            {
                var bios = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (bios != null)
                {
                    info["bios"] = new JsonObject
                    {
                        ["manufacturer"] = bios["Manufacturer"]?.ToString(),
                        ["name"] = bios["Name"]?.ToString(),
                        ["version"] = bios["Version"]?.ToString(),
                        ["serialNumber"] = bios["SerialNumber"]?.ToString(),
                        ["releaseDate"] = bios["ReleaseDate"]?.ToString()
                    };
                }
            }

            // Anakart bilgileri
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
            {
                var board = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (board != null)
                {
                    info["motherboard"] = new JsonObject
                    {
                        ["manufacturer"] = board["Manufacturer"]?.ToString(),
                        ["product"] = board["Product"]?.ToString(),
                        ["serialNumber"] = board["SerialNumber"]?.ToString(),
                        ["version"] = board["Version"]?.ToString()
                    };
                }
            }

            // Bellek modülleri
            var memoryArray = new JsonArray();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
            {
                foreach (ManagementObject memory in searcher.Get())
                {
                    memoryArray.Add(new JsonObject
                    {
                        ["manufacturer"] = memory["Manufacturer"]?.ToString(),
                        ["capacity"] = memory["Capacity"]?.ToString(),
                        ["speed"] = memory["Speed"]?.ToString(),
                        ["deviceLocator"] = memory["DeviceLocator"]?.ToString(),
                        ["partNumber"] = memory["PartNumber"]?.ToString()
                    });
                }
            }
            info["memory"] = memoryArray;
        }
        catch (Exception ex)
        {
            info["error"] = $"SMBIOS query failed: {ex.Message}";
        }

        return info;
    }

    private async Task HandleVMDetectionAsync(AgentCommand command, AgentContext context)
    {
        var vmInfo = await Task.Run(DetectVirtualMachine).ConfigureAwait(false);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            vmInfo)).ConfigureAwait(false);
    }

    private static JsonObject DetectVirtualMachine()
    {
        var info = new JsonObject { ["isVirtualMachine"] = false };

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            var system = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (system != null)
            {
                var manufacturer = system["Manufacturer"]?.ToString()?.ToLowerInvariant() ?? "";
                var model = system["Model"]?.ToString()?.ToLowerInvariant() ?? "";

                var vmIndicators = new[] { "vmware", "virtualbox", "virtual", "qemu", "kvm", "xen", "hyper-v", "parallels" };
                var isVM = vmIndicators.Any(indicator => manufacturer.Contains(indicator) || model.Contains(indicator));

                info["isVirtualMachine"] = isVM;
                info["manufacturer"] = system["Manufacturer"]?.ToString();
                info["model"] = system["Model"]?.ToString();

                if (isVM)
                {
                    if (manufacturer.Contains("vmware") || model.Contains("vmware"))
                        info["hypervisor"] = "VMware";
                    else if (manufacturer.Contains("virtualbox") || model.Contains("virtualbox"))
                        info["hypervisor"] = "VirtualBox";
                    else if (manufacturer.Contains("qemu") || model.Contains("qemu"))
                        info["hypervisor"] = "QEMU";
                    else if (manufacturer.Contains("microsoft") && model.Contains("virtual"))
                        info["hypervisor"] = "Hyper-V";
                    else if (manufacturer.Contains("xen") || model.Contains("xen"))
                        info["hypervisor"] = "Xen";
                    else if (manufacturer.Contains("parallels"))
                        info["hypervisor"] = "Parallels";
                    else
                        info["hypervisor"] = "Unknown";
                }
            }
        }
        catch (Exception ex)
        {
            info["error"] = $"VM detection failed: {ex.Message}";
        }

        return info;
    }

    private async Task HandleWiFiScanAsync(AgentCommand command, AgentContext context)
    {
        var networks = await Task.Run(ScanWiFiNetworks).ConfigureAwait(false);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            new JsonObject { ["networks"] = networks })).ConfigureAwait(false);
    }

    private static JsonArray ScanWiFiNetworks()
    {
        var networks = new JsonArray();

        if (!OperatingSystem.IsWindows())
        {
            networks.Add(new JsonObject { ["error"] = "WiFi scan is only supported on Windows" });
            return networks;
        }

        try
        {
            // netsh wlan show networks mode=bssid kullanarak WiFi ağlarını tarama
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = "wlan show networks mode=bssid",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                networks.Add(new JsonObject { ["error"] = "Failed to start netsh process" });
                return networks;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                networks.Add(new JsonObject { ["error"] = "netsh command failed" });
                return networks;
            }

            // Basit parsing (her SSID için bir entry)
            var lines = output.Split('\n');
            JsonObject? currentNetwork = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentNetwork != null)
                    {
                        networks.Add(currentNetwork);
                    }

                    var ssid = trimmed.Split(':').Length > 1 ? trimmed.Split(':')[1].Trim() : "";
                    currentNetwork = new JsonObject { ["ssid"] = ssid };
                }
                else if (currentNetwork != null)
                {
                    if (trimmed.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
                    {
                        var signal = trimmed.Split(':').Length > 1 ? trimmed.Split(':')[1].Trim() : "";
                        currentNetwork["signal"] = signal;
                    }
                    else if (trimmed.StartsWith("Authentication", StringComparison.OrdinalIgnoreCase))
                    {
                        var auth = trimmed.Split(':').Length > 1 ? trimmed.Split(':')[1].Trim() : "";
                        currentNetwork["authentication"] = auth;
                    }
                    else if (trimmed.StartsWith("Encryption", StringComparison.OrdinalIgnoreCase))
                    {
                        var encryption = trimmed.Split(':').Length > 1 ? trimmed.Split(':')[1].Trim() : "";
                        currentNetwork["encryption"] = encryption;
                    }
                    else if (trimmed.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                    {
                        var bssid = trimmed.Split(':').Length > 1 ? string.Join(":", trimmed.Split(':').Skip(1)).Trim() : "";
                        currentNetwork["bssid"] = bssid;
                    }
                }
            }

            if (currentNetwork != null)
            {
                networks.Add(currentNetwork);
            }
        }
        catch (Exception ex)
        {
            networks.Add(new JsonObject { ["error"] = $"WiFi scan failed: {ex.Message}" });
        }

        return networks;
    }

    private async Task HandlePerfCountersAsync(AgentCommand command, AgentContext context)
    {
        var counters = await Task.Run(GetPerformanceCounters).ConfigureAwait(false);
        await context.ResponseWriter.SendAsync(new CommandResult(
            command.Action,
            command.NodeId,
            command.SessionId,
            counters)).ConfigureAwait(false);
    }

    private static JsonObject GetPerformanceCounters()
    {
        var counters = new JsonObject();

        if (!OperatingSystem.IsWindows())
        {
            counters["error"] = "Performance counters are only supported on Windows";
            return counters;
        }

        try
        {
            // CPU kullanımı
            using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
            {
                cpuCounter.NextValue(); // İlk çağrı
                System.Threading.Thread.Sleep(100);
                counters["cpuUsagePercent"] = Math.Round(cpuCounter.NextValue(), 2);
            }

            // Bellek kullanımı
            using (var memAvailable = new PerformanceCounter("Memory", "Available MBytes"))
            {
                counters["memoryAvailableMB"] = Math.Round(memAvailable.NextValue(), 2);
            }

            // Disk okuma/yazma
            var diskReads = new JsonArray();
            var diskWrites = new JsonArray();

            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name != '_Total'"))
            {
                foreach (ManagementObject disk in searcher.Get())
                {
                    var diskName = disk["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(diskName))
                    {
                        try
                        {
                            using var readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", diskName);
                            using var writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", diskName);

                            diskReads.Add(new JsonObject
                            {
                                ["disk"] = diskName,
                                ["bytesPerSec"] = Math.Round(readCounter.NextValue(), 2)
                            });

                            diskWrites.Add(new JsonObject
                            {
                                ["disk"] = diskName,
                                ["bytesPerSec"] = Math.Round(writeCounter.NextValue(), 2)
                            });
                        }
                        catch
                        {
                            // Bazı diskler için counter mevcut olmayabilir
                        }
                    }
                }
            }

            counters["diskReads"] = diskReads;
            counters["diskWrites"] = diskWrites;

            // Network kullanımı
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            var netStats = new JsonArray();
            foreach (var ni in networkInterfaces)
            {
                var stats = ni.GetIPv4Statistics();
                netStats.Add(new JsonObject
                {
                    ["interface"] = ni.Name,
                    ["bytesSent"] = stats.BytesSent,
                    ["bytesReceived"] = stats.BytesReceived
                });
            }
            counters["networkInterfaces"] = netStats;
        }
        catch (Exception ex)
        {
            counters["error"] = $"Performance counter query failed: {ex.Message}";
        }

        return counters;
    }
}
