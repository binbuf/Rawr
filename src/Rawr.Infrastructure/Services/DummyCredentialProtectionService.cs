using Rawr.Core.Interfaces;

namespace Rawr.Infrastructure.Services;

public class DummyCredentialProtectionService : ICredentialProtectionService
{
    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string encrypted) => encrypted;
}
