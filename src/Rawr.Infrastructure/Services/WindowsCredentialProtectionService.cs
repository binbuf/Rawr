using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public class WindowsCredentialProtectionService : ICredentialProtectionService
{
    private const string ProtectedPrefix = "DPAPI:";

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return ProtectedPrefix + Convert.ToBase64String(encrypted);
    }

    public string Unprotect(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
            return encrypted;

        if (!encrypted.StartsWith(ProtectedPrefix))
            return encrypted; // Plaintext fallback for migration

        try
        {
            var base64 = encrypted[ProtectedPrefix.Length..];
            var encryptedBytes = Convert.FromBase64String(base64);
            var decrypted = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            // If decryption fails, return as-is (might be plaintext)
            return encrypted;
        }
    }
}
