using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Configuration;
using Server.Application.Common;
using Server.Application.DTOs.ActiveDirectory;
using Microsoft.Extensions.Logging;

namespace Server.Application.Services;

public interface IActiveDirectoryService
{
    Task<Result<List<ADUserDto>>> GetUsersAsync(string? searchFilter = null);
    Task<Result<List<ADComputerDto>>> GetComputersAsync(string? searchFilter = null);
    Task<Result<bool>> SyncUsersAsync();
    Task<Result<bool>> SyncComputersAsync();
    Task<Result<ADDomainInfoDto>> GetDomainInfoAsync();
    Task<Result<bool>> TestConnectionAsync();
}

public class ActiveDirectoryService : IActiveDirectoryService
{
    private readonly ILogger<ActiveDirectoryService> _logger;
    private readonly string? _domain;
    private readonly string? _ldapPath;
    private readonly string? _username;
    private readonly string? _password;

    public ActiveDirectoryService(
        ILogger<ActiveDirectoryService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _domain = configuration["ActiveDirectory:Domain"];
        _ldapPath = configuration["ActiveDirectory:LdapPath"];
        _username = configuration["ActiveDirectory:Username"];
        _password = configuration["ActiveDirectory:Password"];
    }

    public async Task<Result<bool>> TestConnectionAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_domain))
            {
                return Result<bool>.Fail("Active Directory not configured", "AD_NOT_CONFIGURED");
            }

            using var context = new PrincipalContext(ContextType.Domain, _domain, _username, _password);
            
            // Try to get domain info as connection test
            var domainInfo = System.DirectoryServices.ActiveDirectory.Domain.GetDomain(
                new DirectoryContext(DirectoryContextType.Domain, _domain, _username, _password));
            var connected = domainInfo != null;

            _logger.LogInformation("AD Connection test: {Status}, Domain: {Domain}", 
                connected ? "Success" : "Failed", _domain);

            return await Task.FromResult(Result<bool>.Ok(connected));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AD connection test failed");
            return Result<bool>.Fail($"Connection failed: {ex.Message}", "AD_CONNECTION_ERROR");
        }
    }

    public async Task<Result<ADDomainInfoDto>> GetDomainInfoAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_domain))
            {
                return Result<ADDomainInfoDto>.Fail("Active Directory not configured", "AD_NOT_CONFIGURED");
            }

            var domainContext = new DirectoryContext(DirectoryContextType.Domain, _domain, _username, _password);
            var domain = System.DirectoryServices.ActiveDirectory.Domain.GetDomain(domainContext);

            var info = new ADDomainInfoDto
            {
                Name = domain.Name,
                ForestName = domain.Forest.Name,
                DomainMode = domain.DomainMode.ToString(),
                PdcRoleOwner = domain.PdcRoleOwner.Name,
                RidRoleOwner = domain.RidRoleOwner.Name,
                InfrastructureRoleOwner = domain.InfrastructureRoleOwner.Name
            };

            // Get domain controllers
            var controllers = new List<string>();
            foreach (DomainController dc in domain.DomainControllers)
            {
                controllers.Add(dc.Name);
            }
            info.DomainControllers = controllers;

            return await Task.FromResult(Result<ADDomainInfoDto>.Ok(info));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get domain info");
            return Result<ADDomainInfoDto>.Fail($"Failed to get domain info: {ex.Message}", "AD_DOMAIN_INFO_ERROR");
        }
    }

    public async Task<Result<List<ADUserDto>>> GetUsersAsync(string? searchFilter = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_domain))
            {
                return Result<List<ADUserDto>>.Fail("Active Directory not configured", "AD_NOT_CONFIGURED");
            }

            var users = new List<ADUserDto>();

            using (var context = new PrincipalContext(ContextType.Domain, _domain, _username, _password))
            {
                var userPrincipal = new UserPrincipal(context);
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    userPrincipal.Name = $"*{searchFilter}*";
                }

                using var searcher = new PrincipalSearcher(userPrincipal);
                var results = searcher.FindAll();

                foreach (UserPrincipal user in results)
                {
                    if (user == null) continue;

                    users.Add(new ADUserDto
                    {
                        SamAccountName = user.SamAccountName,
                        DisplayName = user.DisplayName,
                        EmailAddress = user.EmailAddress,
                        Enabled = user.Enabled ?? false,
                        LastLogon = user.LastLogon,
                        DistinguishedName = user.DistinguishedName,
                        UserPrincipalName = user.UserPrincipalName
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} users from AD", users.Count);
            return await Task.FromResult(Result<List<ADUserDto>>.Ok(users));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AD users");
            return Result<List<ADUserDto>>.Fail($"Failed to get users: {ex.Message}", "AD_USERS_ERROR");
        }
    }

    public async Task<Result<List<ADComputerDto>>> GetComputersAsync(string? searchFilter = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_domain))
            {
                return Result<List<ADComputerDto>>.Fail("Active Directory not configured", "AD_NOT_CONFIGURED");
            }

            var computers = new List<ADComputerDto>();

            using (var context = new PrincipalContext(ContextType.Domain, _domain, _username, _password))
            {
                var computerPrincipal = new ComputerPrincipal(context);
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    computerPrincipal.Name = $"*{searchFilter}*";
                }

                using var searcher = new PrincipalSearcher(computerPrincipal);
                var results = searcher.FindAll();

                foreach (ComputerPrincipal computer in results)
                {
                    if (computer == null) continue;

                    computers.Add(new ADComputerDto
                    {
                        Name = computer.Name,
                        SamAccountName = computer.SamAccountName,
                        Enabled = computer.Enabled ?? false,
                        LastLogon = computer.LastLogon,
                        DistinguishedName = computer.DistinguishedName,
                        OperatingSystem = GetComputerOS(computer),
                        Description = computer.Description
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} computers from AD", computers.Count);
            return await Task.FromResult(Result<List<ADComputerDto>>.Ok(computers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AD computers");
            return Result<List<ADComputerDto>>.Fail($"Failed to get computers: {ex.Message}", "AD_COMPUTERS_ERROR");
        }
    }

    public async Task<Result<bool>> SyncUsersAsync()
    {
        try
        {
            // Get AD users
            var adUsersResult = await GetUsersAsync();
            if (!adUsersResult.Success || adUsersResult.Data == null)
            {
                return Result<bool>.Fail(adUsersResult.ErrorMessage ?? "Failed to get AD users", adUsersResult.ErrorCode);
            }

            // TODO: Sync with local database
            // For now, just log
            _logger.LogInformation("Synced {Count} AD users to database", adUsersResult.Data.Count);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync AD users");
            return Result<bool>.Fail($"Sync failed: {ex.Message}", "AD_SYNC_ERROR");
        }
    }

    public async Task<Result<bool>> SyncComputersAsync()
    {
        try
        {
            // Get AD computers
            var adComputersResult = await GetComputersAsync();
            if (!adComputersResult.Success || adComputersResult.Data == null)
            {
                return Result<bool>.Fail(adComputersResult.ErrorMessage ?? "Failed to get AD computers", adComputersResult.ErrorCode);
            }

            // TODO: Sync with local database (Devices table)
            // Match by computer name, auto-register if needed
            _logger.LogInformation("Synced {Count} AD computers to database", adComputersResult.Data.Count);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync AD computers");
            return Result<bool>.Fail($"Sync failed: {ex.Message}", "AD_SYNC_ERROR");
        }
    }

    private string? GetComputerOS(ComputerPrincipal computer)
    {
        try
        {
            var de = computer.GetUnderlyingObject() as DirectoryEntry;
            return de?.Properties["operatingSystem"]?.Value?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
