using System.Security.Cryptography;
using System.Text;
using MarksBaseballCards.AdminBootstrap;
using Xunit;

namespace MarksBaseballCards.Tests;

public sealed class AdminCredentialPackageTests
{
    private const string PackagePassphrase = "Different-fixture-package-key!2026";
    private static AdminCredentials Credentials => AdminCredentials.Create("private-owner@example.test", "Fixture-only-admin-password!2026");

    [Fact]
    public void EncryptsEmailAndHashAndOnlyCorrectPassphraseRestoresThem()
    {
        var credentials = Credentials;
        var package = AdminCredentialPackage.Encrypt(credentials, PackagePassphrase);
        var restored = AdminCredentialPackage.Decrypt(package, PackagePassphrase);
        Assert.Equal(credentials, restored);
        Assert.DoesNotContain(credentials.Email, Encoding.UTF8.GetString(package));
        Assert.DoesNotContain(credentials.PasswordHash, Encoding.UTF8.GetString(package));
        Assert.DoesNotContain("Fixture-only-admin-password!2026", Encoding.UTF8.GetString(package));
        Assert.ThrowsAny<CryptographicException>(() => AdminCredentialPackage.Decrypt(package, "Incorrect-package-key!2026"));
        Assert.False(package.SequenceEqual(AdminCredentialPackage.Encrypt(credentials, PackagePassphrase)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(25)]
    [InlineData(37)]
    [InlineData(53)]
    public void RejectsTamperedVersionSaltNonceTagOrCiphertext(int position)
    {
        var package = AdminCredentialPackage.Encrypt(Credentials, PackagePassphrase);
        package[position] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => AdminCredentialPackage.Decrypt(package, PackagePassphrase));
    }

    [Fact]
    public void RejectsTruncatedOversizedAndWeaklyProtectedPackages()
    {
        Assert.ThrowsAny<CryptographicException>(() => AdminCredentialPackage.Decrypt([], PackagePassphrase));
        Assert.ThrowsAny<CryptographicException>(() => AdminCredentialPackage.Decrypt(new byte[AdminCredentialPackage.MaximumBytes + 1], PackagePassphrase));
        Assert.Throws<ArgumentException>(() => AdminCredentialPackage.Encrypt(Credentials, "short"));
    }
}
