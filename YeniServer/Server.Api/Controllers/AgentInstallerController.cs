using Microsoft.AspNetCore.Mvc;
using Server.Api.Attributes;
using Server.Application.Interfaces;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAdmin]
public class AgentInstallerController : ControllerBase
{
    private readonly ILogger<AgentInstallerController> _logger;
    private readonly IConfiguration _configuration;

    public AgentInstallerController(
        ILogger<AgentInstallerController> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Download customized agent installer (EXE)
    /// </summary>
    [HttpGet("download/windows")]
    public async Task<IActionResult> DownloadWindowsInstaller(
        [FromQuery] string? deviceName = null,
        [FromQuery] string? groupName = null,
        [FromQuery] string? enrollmentKey = null)
    {
        try
        {
            var serverUrl = _configuration["ServerUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var agentConfig = BuildAgentConfig(serverUrl, deviceName, groupName, enrollmentKey);
            
            // Get base agent path
            var basePath = Path.Combine(AppContext.BaseDirectory, "AgentInstallers", "AgentHost.exe");
            if (!System.IO.File.Exists(basePath))
            {
                return NotFound(new { error = "Base agent installer not found. Please build and place AgentHost.exe in AgentInstallers folder." });
            }

            // Create config file
            var configJson = JsonSerializer.Serialize(agentConfig, new JsonSerializerOptions { WriteIndented = true });
            var configBytes = Encoding.UTF8.GetBytes(configJson);

            // Create a ZIP package with agent + config
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                // Add agent executable
                var agentEntry = archive.CreateEntry("AgentHost.exe");
                using (var entryStream = agentEntry.Open())
                using (var fileStream = System.IO.File.OpenRead(basePath))
                {
                    await fileStream.CopyToAsync(entryStream);
                }

                // Add config file
                var configEntry = archive.CreateEntry("agentconfig.json");
                using (var entryStream = configEntry.Open())
                {
                    await entryStream.WriteAsync(configBytes);
                }

                // Add installation script
                var installScript = GenerateWindowsInstallScript(deviceName, groupName);
                var installEntry = archive.CreateEntry("install.bat");
                using (var entryStream = installEntry.Open())
                {
                    var scriptBytes = Encoding.UTF8.GetBytes(installScript);
                    await entryStream.WriteAsync(scriptBytes);
                }

                // Add README
                var readme = GenerateReadme(serverUrl, deviceName, groupName);
                var readmeEntry = archive.CreateEntry("README.txt");
                using (var entryStream = readmeEntry.Open())
                {
                    var readmeBytes = Encoding.UTF8.GetBytes(readme);
                    await entryStream.WriteAsync(readmeBytes);
                }
            }

            memoryStream.Position = 0;
            var fileName = $"OlmezAgent_{deviceName ?? "Default"}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
            
            _logger.LogInformation("Agent installer downloaded: {FileName}", fileName);
            
            return File(memoryStream.ToArray(), "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating agent installer");
            return StatusCode(500, new { error = "Failed to generate installer", details = ex.Message });
        }
    }

    /// <summary>
    /// Download GPO deployment package
    /// </summary>
    [HttpGet("download/gpo")]
    [RequiresFeature(Domain.Enums.EnterpriseFeature.ActiveDirectory)]
    public async Task<IActionResult> DownloadGPOPackage(
        [FromQuery] string domainName,
        [FromQuery] string? groupName = null)
    {
        try
        {
            var serverUrl = _configuration["ServerUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                // GPO deployment script
                var gpoScript = GenerateGPODeploymentScript(serverUrl, domainName, groupName);
                var scriptEntry = archive.CreateEntry("Deploy-OlmezAgent.ps1");
                using (var entryStream = scriptEntry.Open())
                {
                    var scriptBytes = Encoding.UTF8.GetBytes(gpoScript);
                    await entryStream.WriteAsync(scriptBytes);
                }

                // MSI package info (placeholder - actual MSI would need WiX toolset)
                var msiInfo = GenerateMSIInfo(serverUrl, domainName);
                var infoEntry = archive.CreateEntry("MSI_INSTRUCTIONS.txt");
                using (var entryStream = infoEntry.Open())
                {
                    var infoBytes = Encoding.UTF8.GetBytes(msiInfo);
                    await entryStream.WriteAsync(infoBytes);
                }

                // GPO import XML
                var gpoXml = GenerateGPOXml(domainName);
                var xmlEntry = archive.CreateEntry("GPO_Settings.xml");
                using (var entryStream = xmlEntry.Open())
                {
                    var xmlBytes = Encoding.UTF8.GetBytes(gpoXml);
                    await entryStream.WriteAsync(xmlBytes);
                }
            }

            memoryStream.Position = 0;
            var fileName = $"OlmezAgent_GPO_{domainName}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
            
            _logger.LogInformation("GPO package downloaded for domain: {Domain}", domainName);
            
            return File(memoryStream.ToArray(), "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating GPO package");
            return StatusCode(500, new { error = "Failed to generate GPO package", details = ex.Message });
        }
    }

    private object BuildAgentConfig(string serverUrl, string? deviceName, string? groupName, string? enrollmentKey)
    {
        return new
        {
            ServerEndpoint = $"{serverUrl}/agent.ashx".Replace("http://", "ws://").Replace("https://", "wss://"),
            DeviceId = deviceName ?? Environment.MachineName,
            GroupName = groupName ?? "Default",
            EnrollmentKey = enrollmentKey,
            CertificatePinning = new
            {
                Enabled = false // Can be configured per deployment
            },
            Logging = new
            {
                Level = "Information",
                FilePath = "logs/agent.log"
            }
        };
    }

    private string GenerateWindowsInstallScript(string? deviceName, string? groupName)
    {
        return $@"@echo off
REM Olmez Agent Installation Script
REM Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

echo Installing Olmez Agent...
echo Device: {deviceName ?? "Auto-detected"}
echo Group: {groupName ?? "Default"}
echo.

REM Check admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

REM Create installation directory
set INSTALL_DIR=%ProgramFiles%\OlmezAgent
if not exist ""%INSTALL_DIR%"" mkdir ""%INSTALL_DIR%""

REM Copy files
echo Copying agent files...
copy /Y AgentHost.exe ""%INSTALL_DIR%\AgentHost.exe""
copy /Y agentconfig.json ""%INSTALL_DIR%\agentconfig.json""

REM Create Windows Service
echo Creating Windows Service...
sc create OlmezAgent binPath= ""%INSTALL_DIR%\AgentHost.exe"" DisplayName= ""Olmez Remote Management Agent"" start= auto
sc description OlmezAgent ""Olmez remote management and monitoring agent""

REM Start service
echo Starting service...
sc start OlmezAgent

echo.
echo Installation complete!
echo Service Status:
sc query OlmezAgent
echo.
pause
";
    }

    private string GenerateReadme(string serverUrl, string? deviceName, string? groupName)
    {
        return $@"Olmez Agent Installer Package
Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

SERVER INFORMATION
==================
Server URL: {serverUrl}
Device Name: {deviceName ?? "Auto-detected"}
Group: {groupName ?? "Default"}

INSTALLATION INSTRUCTIONS
=========================

Windows Installation:
1. Extract this ZIP file to a folder
2. Right-click 'install.bat' and select 'Run as Administrator'
3. Wait for the installation to complete
4. Agent will start automatically as a Windows Service

Manual Installation:
1. Copy AgentHost.exe and agentconfig.json to C:\Program Files\OlmezAgent\
2. Run: sc create OlmezAgent binPath= ""C:\Program Files\OlmezAgent\AgentHost.exe"" start= auto
3. Run: sc start OlmezAgent

Verification:
1. Check service status: sc query OlmezAgent
2. Check logs: C:\Program Files\OlmezAgent\logs\agent.log
3. Verify connection in server dashboard

TROUBLESHOOTING
===============
- If agent doesn't connect, check firewall rules
- Verify server URL in agentconfig.json
- Check Windows Event Viewer for errors
- Review agent logs in logs folder

SUPPORT
=======
Documentation: {serverUrl}/docs
Support: admin@olmez.local
";
    }

    private string GenerateGPODeploymentScript(string serverUrl, string domainName, string? groupName)
    {
        return $@"# Olmez Agent GPO Deployment Script
# Domain: {domainName}
# Group: {groupName ?? "Default"}
# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

[CmdletBinding()]
param(
    [string]$ServerUrl = ""{serverUrl}"",
    [string]$GroupName = ""{groupName ?? "Default"}"",
    [string]$InstallPath = ""$env:ProgramFiles\OlmezAgent""
)

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {{
    Write-Error ""This script must be run as Administrator""
    exit 1
}}

Write-Host ""Deploying Olmez Agent via GPO..."" -ForegroundColor Green
Write-Host ""Server: $ServerUrl""
Write-Host ""Group: $GroupName""
Write-Host ""Install Path: $InstallPath""

# Create installation directory
if (-not (Test-Path $InstallPath)) {{
    New-Item -Path $InstallPath -ItemType Directory -Force | Out-Null
}}

# Copy agent executable (assume it's in GPO's Scripts folder)
$sourcePath = ""\\{{0}}\SYSVOL\{{0}}\scripts\OlmezAgent"" -f $env:USERDNSDOMAIN
if (Test-Path ""$sourcePath\AgentHost.exe"") {{
    Copy-Item ""$sourcePath\AgentHost.exe"" -Destination ""$InstallPath\AgentHost.exe"" -Force
    Write-Host ""Agent executable copied"" -ForegroundColor Green
}} else {{
    Write-Warning ""Source agent not found: $sourcePath\AgentHost.exe""
}}

# Create config file
$config = @{{
    ServerEndpoint = $ServerUrl.Replace(""http://"", ""ws://"").Replace(""https://"", ""wss://"") + ""/agent.ashx""
    DeviceId = $env:COMPUTERNAME
    GroupName = $GroupName
    CertificatePinning = @{{ Enabled = $false }}
    Logging = @{{ Level = ""Information""; FilePath = ""logs/agent.log"" }}
}} | ConvertTo-Json -Depth 10

$config | Out-File -FilePath ""$InstallPath\agentconfig.json"" -Encoding UTF8 -Force

# Install Windows Service
$serviceName = ""OlmezAgent""
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($service) {{
    Write-Host ""Service already exists, updating..."" -ForegroundColor Yellow
    Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}} else {{
    Write-Host ""Creating Windows Service..."" -ForegroundColor Green
    New-Service -Name $serviceName `
        -BinaryPathName ""$InstallPath\AgentHost.exe"" `
        -DisplayName ""Olmez Remote Management Agent"" `
        -Description ""Olmez remote management and monitoring agent"" `
        -StartupType Automatic
}}

# Start service
Start-Service $serviceName
Write-Host ""Service started successfully"" -ForegroundColor Green

# Verify
$service = Get-Service -Name $serviceName
Write-Host ""Service Status: $($service.Status)"" -ForegroundColor Cyan

Write-Host ""Deployment completed successfully!"" -ForegroundColor Green
";
    }

    private string GenerateMSIInfo(string serverUrl, string domainName)
    {
        return $@"MSI PACKAGE CREATION INSTRUCTIONS
Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

OVERVIEW
========
To create an MSI package for GPO deployment, you need WiX Toolset.

PREREQUISITES
=============
1. Install WiX Toolset: https://wixtoolset.org/
2. Visual Studio with WiX extension

STEPS TO CREATE MSI
===================
1. Create a WiX project in Visual Studio
2. Add AgentHost.exe and agentconfig.json to the project
3. Configure installation directory: [ProgramFilesFolder]\OlmezAgent
4. Add Windows Service installation component
5. Build the MSI package

SAMPLE WiX XML
==============
<?xml version=""1.0"" encoding=""UTF-8""?>
<Wix xmlns=""http://schemas.microsoft.com/wix/2006/wi"">
  <Product Id=""*"" Name=""Olmez Agent"" 
           Language=""1033"" Version=""2.0.0.0"" 
           Manufacturer=""Olmez Technologies"" 
           UpgradeCode=""YOUR-GUID-HERE"">
    
    <Package InstallerVersion=""200"" Compressed=""yes"" 
             InstallScope=""perMachine"" />
    
    <Directory Id=""TARGETDIR"" Name=""SourceDir"">
      <Directory Id=""ProgramFilesFolder"">
        <Directory Id=""INSTALLFOLDER"" Name=""OlmezAgent"" />
      </Directory>
    </Directory>
    
    <ComponentGroup Id=""ProductComponents"" Directory=""INSTALLFOLDER"">
      <Component Id=""AgentExecutable"">
        <File Id=""AgentHost.exe"" Source=""AgentHost.exe"" KeyPath=""yes"" />
      </Component>
      <Component Id=""AgentConfig"">
        <File Id=""agentconfig.json"" Source=""agentconfig.json"" />
      </Component>
      <Component Id=""AgentService"">
        <ServiceInstall Id=""OlmezAgentService""
                       Type=""ownProcess""
                       Name=""OlmezAgent""
                       DisplayName=""Olmez Remote Management Agent""
                       Description=""Olmez remote management and monitoring agent""
                       Start=""auto""
                       Account=""LocalSystem""
                       ErrorControl=""normal"" />
        <ServiceControl Id=""StartService""
                       Start=""install""
                       Stop=""both""
                       Remove=""uninstall""
                       Name=""OlmezAgent""
                       Wait=""yes"" />
      </Component>
    </ComponentGroup>
    
    <Feature Id=""ProductFeature"" Title=""Olmez Agent"" Level=""1"">
      <ComponentGroupRef Id=""ProductComponents"" />
    </Feature>
  </Product>
</Wix>

GPO DEPLOYMENT
==============
1. Copy the MSI to: \\{domainName}\SYSVOL\{domainName}\scripts\OlmezAgent\
2. Open Group Policy Management Console (gpmc.msc)
3. Create or edit a GPO
4. Navigate to: Computer Configuration -> Policies -> Software Settings -> Software Installation
5. Right-click -> New -> Package
6. Select the MSI from SYSVOL path
7. Choose ""Assigned"" deployment method
8. Link the GPO to desired OUs
9. Run 'gpupdate /force' on target computers

VERIFICATION
============
1. Check installed programs on target computers
2. Verify service is running: Get-Service OlmezAgent
3. Check agent logs
4. Confirm devices appear in server dashboard

SERVER CONFIGURATION
====================
Server URL: {serverUrl}
Domain: {domainName}
WebSocket Endpoint: {serverUrl}/agent.ashx
";
    }

    private string GenerateGPOXml(string domainName)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- Olmez Agent GPO Configuration -->
<!-- Domain: {domainName} -->
<!-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC -->

<GPO>
  <Name>Deploy Olmez Agent</Name>
  <Description>Automatically deploys Olmez remote management agent to domain computers</Description>
  
  <ComputerConfiguration>
    <SoftwareInstallation>
      <Package>
        <Name>Olmez Agent</Name>
        <Path>\\{domainName}\SYSVOL\{domainName}\scripts\OlmezAgent\OlmezAgent.msi</Path>
        <DeploymentType>Assigned</DeploymentType>
        <InstallOnBootup>true</InstallOnBootup>
      </Package>
    </SoftwareInstallation>
    
    <Scripts>
      <Startup>
        <Script>
          <Name>Deploy-OlmezAgent.ps1</Name>
          <Path>\\{domainName}\SYSVOL\{domainName}\scripts\OlmezAgent\Deploy-OlmezAgent.ps1</Path>
          <Parameters></Parameters>
        </Script>
      </Startup>
    </Scripts>
    
    <WindowsFirewall>
      <Rule>
        <Name>Olmez Agent Outbound</Name>
        <Direction>Outbound</Direction>
        <Protocol>TCP</Protocol>
        <LocalPort>Any</LocalPort>
        <RemotePort>5236,443,80</RemotePort>
        <Action>Allow</Action>
      </Rule>
    </WindowsFirewall>
  </ComputerConfiguration>
</GPO>
";
    }
}
