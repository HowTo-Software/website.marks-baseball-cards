using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MarksBaseballCards.AdminBootstrap;

internal static class BootstrapProgram
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && args[0] is not ("create" or "apply")) || Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Run interactively with create or apply. Never pass passwords as arguments or redirected input.");
            return 1;
        }

        var root = new DirectoryInfo(Environment.CurrentDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "MarksBaseballCards.slnx"))) root = root.Parent;
        if (root is null) { Console.Error.WriteLine("Run from inside this repository."); return 1; }

        try
        {
            var output = Path.Combine(root.FullName, "artifacts", "admin-bootstrap.enc");
            if (args.Length == 1 && args[0] == "apply") await ApplyAsync(output);
            else await CreateAsync(output);
            return 0;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("Invalid input. Use a valid email (up to 64 characters), a 16-128 character password and a different 16-128 character package passphrase.");
        }
        catch (Exception ex) when (ex is CryptographicException or System.Text.Json.JsonException or FormatException)
        {
            Console.Error.WriteLine("Could not authenticate the package. Check its passphrase, version and integrity.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Could not access the protected package. Check permissions and whether the output already exists.");
        }
        catch (SqlException)
        {
            Console.Error.WriteLine("Administrator setup was not confirmed. Review the intended database and existing account using a trusted DBA session. Credentials were not logged; do not reset accounts blindly.");
        }
        return 1;
    }

    private static async Task CreateAsync(string output)
    {
        Console.Write("Admin email: ");
        var email = Console.ReadLine() ?? "";
        var password = ReadPassword("Admin password (hidden): ");
        if (password != ReadPassword("Repeat admin password (hidden): ")) throw new ArgumentException("Passwords differ.");
        var passphrase = ReadPassword("Separate package passphrase (hidden): ");
        if (passphrase == password || passphrase != ReadPassword("Repeat package passphrase (hidden): ")) throw new ArgumentException("Package passphrases differ or reuse the admin password.");

        var credentials = AdminCredentials.Create(email, password);
        var encrypted = AdminCredentialPackage.Encrypt(credentials, passphrase);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var stream = new FileStream(output, options))
        {
            if (OperatingSystem.IsWindows())
            {
                var access = new FileSecurity();
                access.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                var owner = WindowsIdentity.GetCurrent().User!;
                access.SetOwner(owner);
                access.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
                new FileInfo(output).SetAccessControl(access);
            }
            // Only authenticated ciphertext reaches the filesystem. No plaintext SQL artifact.
            await stream.WriteAsync(encrypted);
        }
        Console.WriteLine("Created artifacts/admin-bootstrap.enc (AES-256-GCM; ignored by Git).");
        Console.WriteLine("No database was contacted. Keep the package passphrase separate and use apply explicitly when ready.");
    }

    private static async Task ApplyAsync(string path)
    {
        var connectionString = Environment.GetEnvironmentVariable("MARKS_ADMIN_BOOTSTRAP_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Set MARKS_ADMIN_BOOTSTRAP_CONNECTION securely to the intended deployment/DBA connection. No database was contacted.");
            throw new ArgumentException("Missing connection.");
        }
        var settings = new SqlConnectionStringBuilder(connectionString);
        if (settings.InitialCatalog != "Website_Application_MarksBaseballCardsDb"
            || settings.Encrypt.Equals(SqlConnectionEncryptOption.Optional) || settings.TrustServerCertificate || settings.PersistSecurityInfo)
            throw new ArgumentException("The intended database and certificate-validated SQL TLS are required.");

        var encrypted = await ReadBoundedAsync(path);
        var credentials = AdminCredentialPackage.Decrypt(encrypted, ReadPassword("Package passphrase (hidden): "));
        Console.Write("This will create the inventory Admin in the configured database. Type APPLY to continue: ");
        if (Console.ReadLine() != "APPLY") { Console.WriteLine("Cancelled. No database was contacted."); return; }

        await using var template = typeof(BootstrapProgram).Assembly.GetManifestResourceStream("bootstrap-admin.sql")
            ?? throw new IOException("Reviewed SQL resource is missing.");
        using var reader = new StreamReader(template);
        var sql = await reader.ReadToEndAsync();
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        using var command = AdminBootstrapCommand.Create(connection, sql, credentials);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Administrator setup completed. An existing Admin is left unchanged. No plaintext credential file was created.");
    }

    private static async Task<byte[]> ReadBoundedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[AdminCredentialPackage.MaximumBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length))) > 0) length += read;
        if (length > AdminCredentialPackage.MaximumBytes) throw new CryptographicException("Package is oversized.");
        return buffer[..length];
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var result = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return result.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (result.Length > 0) result.Length--; continue; }
            if (!char.IsControl(key.KeyChar) && result.Length < 129) result.Append(key.KeyChar);
        }
    }
}
