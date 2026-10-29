using Microsoft.AspNetCore.Identity;

namespace ResidentPortal.Security;

public sealed class PasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly string _dummyHash;

    public PasswordService()
    {
        _dummyHash = _hasher.HashPassword(new object(), "resident-portal-timing-equalizer");
    }

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(new object(), passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public void VerifyDummy(string password) => Verify(_dummyHash, password);
}
