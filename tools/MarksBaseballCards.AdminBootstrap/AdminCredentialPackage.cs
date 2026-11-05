using System.Security.Cryptography;
using System.Text.Json;

namespace MarksBaseballCards.AdminBootstrap;

public static class AdminCredentialPackage
{
    private static ReadOnlySpan<byte> Magic => "HTSADMIN1"u8;
    private const int SaltLength = 16, NonceLength = 12, TagLength = 16;
    private const int HeaderLength = 9 + SaltLength + NonceLength + TagLength;
    public const int MaximumBytes = 8192;

    public static byte[] Encrypt(AdminCredentials credentials, string protectionPassphrase)
    {
        credentials.Validate();
        ValidatePassphrase(protectionPassphrase);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(credentials);
        if (plaintext.Length > MaximumBytes - HeaderLength) throw new ArgumentException("Package is too large.");
        var envelope = new byte[HeaderLength + plaintext.Length];
        Magic.CopyTo(envelope);
        var salt = envelope.AsSpan(Magic.Length, SaltLength);
        var nonce = envelope.AsSpan(Magic.Length + SaltLength, NonceLength);
        var tag = envelope.AsSpan(Magic.Length + SaltLength + NonceLength, TagLength);
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);
        var key = DeriveKey(protectionPassphrase, salt);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, plaintext, envelope.AsSpan(HeaderLength), tag, Magic);
            return envelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static AdminCredentials Decrypt(ReadOnlySpan<byte> envelope, string protectionPassphrase)
    {
        ValidatePassphrase(protectionPassphrase);
        if (envelope.Length <= HeaderLength || envelope.Length > MaximumBytes || !envelope[..Magic.Length].SequenceEqual(Magic))
            throw new CryptographicException("Invalid or unsupported administrator package.");
        var salt = envelope.Slice(Magic.Length, SaltLength);
        var nonce = envelope.Slice(Magic.Length + SaltLength, NonceLength);
        var tag = envelope.Slice(Magic.Length + SaltLength + NonceLength, TagLength);
        var plaintext = new byte[envelope.Length - HeaderLength];
        var key = DeriveKey(protectionPassphrase, salt);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, envelope[HeaderLength..], tag, plaintext, Magic);
            var credentials = JsonSerializer.Deserialize<AdminCredentials>(plaintext)
                ?? throw new CryptographicException("Invalid administrator package.");
            credentials.Validate();
            return credentials;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveKey(string passphrase, ReadOnlySpan<byte> salt) =>
        Rfc2898DeriveBytes.Pbkdf2(passphrase.AsSpan(), salt, 210_000, HashAlgorithmName.SHA512, 32);

    private static void ValidatePassphrase(string passphrase)
    {
        if (passphrase.Length < 16 || passphrase.Length > 128 || passphrase.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a separate package passphrase of 16 to 128 characters.");
    }
}
