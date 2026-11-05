using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using MarksBaseballCards.AdminBootstrap;

namespace MarksBaseballCards.AdminBootstrap;

internal static class BootstrapProgram
{
public static async Task<int> Main(string[] args)
{
if (args.Length != 0 || Console.IsInputRedirected)
{
    Console.Error.WriteLine("Run interactively without arguments. Passwords must not be passed through command-line history or redirected input.");
    return 1;
}

var root = new DirectoryInfo(Environment.CurrentDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "MarksBaseballCards.slnx"))) root = root.Parent;
if (root is null) { Console.Error.WriteLine("Run from inside this repository."); return 1; }

try
{
    Console.Write("Admin email: ");
    var email = Console.ReadLine() ?? "";
    var password = ReadPassword("Password (16+ characters, hidden): ");
    var confirmation = ReadPassword("Repeat password (hidden): ");
    if (password != confirmation) throw new ArgumentException("Passwords do not match.");
    var template = await File.ReadAllTextAsync(Path.Combine(root.FullName, "deploy", "sql", "bootstrap-admin.sql"));
    var sql = AdminSqlGenerator.Generate(template, email, password);
    var directory = Path.Combine(root.FullName, "artifacts");
    Directory.CreateDirectory(directory);
    var output = Path.Combine(directory, "admin-bootstrap.sql");
    var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
    if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    await using (var stream = new FileStream(output, fileOptions))
    {
        if (OperatingSystem.IsWindows())
        {
            var access = new FileSecurity();
            access.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            access.SetOwner(WindowsIdentity.GetCurrent().User!);
            access.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(output).SetAccessControl(access);
        }
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(sql);
    }
    Console.WriteLine("Generated artifacts/admin-bootstrap.sql (email + salted hash only; ignored by Git).");
    Console.WriteLine("No database was contacted. Review and run it with a deployment account, then securely remove the generated copy.");
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex is ArgumentException ? ex.Message : "Could not write the protected SQL file. Check permissions and whether artifacts/admin-bootstrap.sql already exists.");
    return 1;
}

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
