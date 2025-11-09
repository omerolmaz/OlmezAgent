using Microsoft.EntityFrameworkCore;
using Server.Application.Common;
using Server.Application.DTOs.License;
using Server.Application.Interfaces;
using Server.Domain.Entities;
using Server.Domain.Enums;
using Server.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace Server.Application.Services;

public class LicenseService : ILicenseService
{
    private readonly ApplicationDbContext _context;

    public LicenseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<LicenseDto>> GetCurrentLicenseAsync()
    {
        var license = await _context.Licenses
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IssuedAt)
            .FirstOrDefaultAsync();

        if (license == null)
            return Result<LicenseDto>.Fail("No active license found", "LICENSE_NOT_FOUND");

        return Result<LicenseDto>.Ok(MapToDto(license));
    }

    public async Task<Result<LicenseDto>> ValidateLicenseKeyAsync(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return Result<LicenseDto>.Fail("License key is required", "INVALID_KEY");

        // Validate checksum
        if (!ValidateLicenseChecksum(licenseKey))
            return Result<LicenseDto>.Fail("Invalid license key format", "INVALID_CHECKSUM");

        var license = await _context.Licenses
            .FirstOrDefaultAsync(l => l.LicenseKey == licenseKey);

        if (license == null)
            return Result<LicenseDto>.Fail("License key not found", "KEY_NOT_FOUND");

        if (!license.IsActive)
            return Result<LicenseDto>.Fail("License is inactive", "LICENSE_INACTIVE");

        if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTime.UtcNow)
            return Result<LicenseDto>.Fail("License has expired", "LICENSE_EXPIRED");

        return Result<LicenseDto>.Ok(MapToDto(license));
    }

    public async Task<Result<LicenseDto>> GenerateLicenseAsync(GenerateLicenseRequest request)
    {
        var licenseKey = GenerateLicenseKey(request.Edition);

        var features = request.Edition == LicenseEdition.Enterprise
            ? EnterpriseFeature.All
            : EnterpriseFeature.None;

        var license = new License
        {
            Id = Guid.NewGuid(),
            LicenseKey = licenseKey,
            Edition = request.Edition,
            Features = features,
            MaxDevices = request.MaxDevices,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            LicensedTo = request.LicensedTo,
            CompanyName = request.CompanyName,
            Email = request.Email,
            IsActive = true,
            CurrentDeviceCount = 0
        };

        _context.Licenses.Add(license);
        await _context.SaveChangesAsync();

        return Result<LicenseDto>.Ok(MapToDto(license));
    }

    public async Task<Result<bool>> CheckDeviceCapacityAsync()
    {
        var license = await _context.Licenses
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IssuedAt)
            .FirstOrDefaultAsync();

        if (license == null)
            return Result<bool>.Fail("No active license found", "LICENSE_NOT_FOUND");

        var hasCapacity = license.CurrentDeviceCount < license.MaxDevices;
        return Result<bool>.Ok(hasCapacity);
    }

    public async Task<Result<bool>> HasFeatureAsync(EnterpriseFeature feature)
    {
        var license = await _context.Licenses
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IssuedAt)
            .FirstOrDefaultAsync();

        if (license == null)
            return Result<bool>.Fail("No active license found", "LICENSE_NOT_FOUND");

        var hasFeature = license.Features.HasFlag(feature);
        return Result<bool>.Ok(hasFeature);
    }

    public async Task<Result> IncrementDeviceCountAsync()
    {
        var license = await _context.Licenses
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IssuedAt)
            .FirstOrDefaultAsync();

        if (license == null)
            return Result.Fail("No active license found", "LICENSE_NOT_FOUND");

        if (license.CurrentDeviceCount >= license.MaxDevices)
            return Result.Fail("Device limit reached", "DEVICE_LIMIT_EXCEEDED");

        license.CurrentDeviceCount++;
        await _context.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result> DecrementDeviceCountAsync()
    {
        var license = await _context.Licenses
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IssuedAt)
            .FirstOrDefaultAsync();

        if (license == null)
            return Result.Fail("No active license found", "LICENSE_NOT_FOUND");

        if (license.CurrentDeviceCount > 0)
        {
            license.CurrentDeviceCount--;
            await _context.SaveChangesAsync();
        }

        return Result.Ok();
    }

    private string GenerateLicenseKey(LicenseEdition edition)
    {
        var editionPrefix = edition == LicenseEdition.Community ? "COMMUNITY" : "ENTERPRISE";
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 12)
            .ToUpper();

        var baseKey = $"OLMEZ-{editionPrefix}-{randomPart}";
        var checksum = CalculateChecksum(baseKey);

        return $"{baseKey}-{checksum}";
    }

    private bool ValidateLicenseChecksum(string licenseKey)
    {
        var parts = licenseKey.Split('-');
        if (parts.Length != 4)
            return false;

        var baseKey = string.Join("-", parts.Take(3));
        var providedChecksum = parts[3];
        var calculatedChecksum = CalculateChecksum(baseKey);

        return providedChecksum.Equals(calculatedChecksum, StringComparison.OrdinalIgnoreCase);
    }

    private string CalculateChecksum(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 8)
            .ToUpper();
    }

    private LicenseDto MapToDto(License license)
    {
        return new LicenseDto
        {
            Id = license.Id,
            LicenseKey = license.LicenseKey,
            Edition = license.Edition,
            Features = license.Features,
            MaxDevices = license.MaxDevices,
            CurrentDeviceCount = license.CurrentDeviceCount,
            IssuedAt = license.IssuedAt,
            ExpiresAt = license.ExpiresAt,
            LicensedTo = license.LicensedTo,
            CompanyName = license.CompanyName,
            IsActive = license.IsActive
        };
    }
}
