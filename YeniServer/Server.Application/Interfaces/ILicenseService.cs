using Server.Application.Common;
using Server.Application.DTOs.License;
using Server.Domain.Enums;

namespace Server.Application.Interfaces;

public interface ILicenseService
{
    Task<Result<LicenseDto>> GetCurrentLicenseAsync();
    Task<Result<LicenseDto>> ValidateLicenseKeyAsync(string licenseKey);
    Task<Result<LicenseDto>> GenerateLicenseAsync(GenerateLicenseRequest request);
    Task<Result<bool>> CheckDeviceCapacityAsync();
    Task<Result<bool>> HasFeatureAsync(EnterpriseFeature feature);
    Task<Result> IncrementDeviceCountAsync();
    Task<Result> DecrementDeviceCountAsync();
}
