using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Agent.Abstractions;
using Microsoft.Extensions.Logging;

namespace Agent.Transport;

internal static class CertificatePinValidator
{
    public static bool ValidateCertificate(X509Certificate? certificate, AgentRuntimeOptions options, ILogger logger)
    {
        if (certificate == null)
        {
            logger.LogWarning("Sunucu sertifikası alınamadı.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.ServerCertificateHash))
        {
            // Pin belirtilmemişse sertifikayı kabul et.
            return true;
        }

        var expected = Sanitize(options.ServerCertificateHash);
        var actual = ComputeHash(certificate, options.ServerCertificateHashAlgorithm);

        if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Sunucu sertifika karması doğrulandı ({Algorithm}).", options.ServerCertificateHashAlgorithm);
            return true;
        }

        logger.LogError("Sunucu sertifikası pin ile eşleşmiyor. Beklenen: {Expected}, Gelen: {Actual}", expected, actual);
        return false;
    }

    private static string Sanitize(string hash) =>
        hash.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static string ComputeHash(X509Certificate certificate, string algorithm)
    {
        var raw = certificate.GetRawCertData();
        using var hash = CreateHashAlgorithm(algorithm);
        var digest = hash.ComputeHash(raw);
        return string.Concat(digest.Select(b => b.ToString("x2")));
    }

    private static HashAlgorithm CreateHashAlgorithm(string algorithm) =>
        algorithm.ToLowerInvariant() switch
        {
            "sha256" => SHA256.Create(),
            "sha384" => SHA384.Create(),
            "sha512" => SHA512.Create(),
            _ => throw new InvalidOperationException($"Desteklenmeyen algoritma: {algorithm}")
        };
}
