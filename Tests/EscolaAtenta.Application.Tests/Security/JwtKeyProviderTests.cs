using System.Security.AccessControl;
using EscolaAtenta.API.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EscolaAtenta.Application.Tests.Security;

public class JwtKeyProviderTests
{
    [Fact]
    public void GeraChavePersistenteSemModificarConfiguracaoPublica()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ea-jwt-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(directory);
            var publicFile = Path.Combine(directory, "appsettings.json");
            File.WriteAllText(publicFile, "{}");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var first = JwtKeyProvider.Initialize(configuration, directory);
            var second = JwtKeyProvider.Initialize(new ConfigurationBuilder().AddInMemoryCollection().Build(), directory);
            Convert.FromBase64String(first).Should().HaveCount(64);
            second.Should().Be(first);
            configuration["Jwt:SecretKey"].Should().Be(first);
            File.ReadAllText(publicFile).Should().Be("{}");
            if (OperatingSystem.IsWindows())
            {
                var acl = new System.IO.FileInfo(Path.Combine(directory, "Secrets", "jwt.key")).GetAccessControl();
                acl.AreAccessRulesProtected.Should().BeTrue();
                var rules = acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                foreach (System.Security.AccessControl.FileSystemAccessRule r in rules)
                    (
                    r.IdentityReference.Value == "S-1-1-0" || r.IdentityReference.Value == "S-1-5-32-545" || r.IsInherited).Should().BeFalse();
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void RejeitaChaveConfiguradaCurta()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Jwt:SecretKey"] = "curta" }).Build();
        var act = () => JwtKeyProvider.Initialize(configuration, Path.GetTempPath());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PreservaChaveExplicitaSemCriarArquivos()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Jwt:SecretKey"] = key }).Build();
        var directory = Path.Combine(Path.GetTempPath(), "ea-jwt-" + Guid.NewGuid());
        JwtKeyProvider.Initialize(configuration, directory).Should().Be(key);
        Directory.Exists(directory).Should().BeFalse();
    }
}
