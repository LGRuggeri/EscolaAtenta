using System;
using EscolaAtenta.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>
/// Implementação do provedor de Tenant da Escola.
/// Extrai o ID da escola do appsettings.json e armazena em memória para acessos rápidos.
/// 
/// Padrão: Anti-Corruption Layer (evita que o AppDbContext ou o Domínio 
/// conheçam detalhes da IConfiguration do ASP.NET).
/// </summary>
public class EscolaTenantProvider : IEscolaTenantProvider
{
    public Guid EscolaId { get; }

    public EscolaTenantProvider(IConfiguration configuration)
    {
        var idString = configuration["EscolaContext:Id"];
        
        if (string.IsNullOrWhiteSpace(idString) || !Guid.TryParse(idString, out var id) || id == Guid.Empty)
        {
            throw new InvalidOperationException("EscolaContext:Id não está configurado corretamente no appsettings.json ou é um Guid vazio.");
        }

        EscolaId = id;
    }
}
