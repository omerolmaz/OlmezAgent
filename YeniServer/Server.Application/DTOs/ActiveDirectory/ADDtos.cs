namespace Server.Application.DTOs.ActiveDirectory;

public class ADUserDto
{
    public string? SamAccountName { get; set; }
    public string? DisplayName { get; set; }
    public string? EmailAddress { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? DistinguishedName { get; set; }
    public bool Enabled { get; set; }
    public DateTime? LastLogon { get; set; }
}

public class ADComputerDto
{
    public string? Name { get; set; }
    public string? SamAccountName { get; set; }
    public string? DistinguishedName { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public DateTime? LastLogon { get; set; }
}

public class ADDomainInfoDto
{
    public string? Name { get; set; }
    public string? ForestName { get; set; }
    public string? DomainMode { get; set; }
    public string? PdcRoleOwner { get; set; }
    public string? RidRoleOwner { get; set; }
    public string? InfrastructureRoleOwner { get; set; }
    public List<string> DomainControllers { get; set; } = new();
}
