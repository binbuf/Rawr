namespace Rawr.Core.Interfaces;

public interface ICredentialProtectionService
{
    string Protect(string plaintext);
    string Unprotect(string encrypted);
}
