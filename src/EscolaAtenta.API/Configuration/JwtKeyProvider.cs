using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace EscolaAtenta.API.Configuration;

public static class JwtKeyProvider
{
    public static string Initialize(IConfiguration configuration, string baseDirectory)
    {
        var configured = configuration["Jwt:SecretKey"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Validate(configured);

        var directory = Path.Combine(Path.GetFullPath(baseDirectory), "Secrets");
        Directory.CreateDirectory(directory);
        RejectLink(directory);
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in AllowedIdentities())
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(security);
        }
        else
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var path = Path.Combine(directory, "jwt.key");
        if (!File.Exists(path))
        {
            // O diretório já está protegido antes de criar qualquer conteúdo secreto.
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
            stream.Write(bytes);
            stream.Flush(true);
        }
        RejectLink(path);
        if (OperatingSystem.IsWindows())
        {
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in AllowedIdentities())
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var key = Validate(File.ReadAllText(path).Trim());
        configuration["Jwt:SecretKey"] = key;
        return key;
    }

    private static string Validate(string key)
    {
        if (Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("A chave JWT deve ter pelo menos 32 bytes.");
        return key;
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("O armazenamento da chave JWT não pode ser um link.");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static IEnumerable<SecurityIdentifier> AllowedIdentities()
    {
        yield return new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        yield return new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        // Permite execução local sem elevação; no serviço esta identidade é SYSTEM.
        yield return WindowsIdentity.GetCurrent().User!;
    }
}
