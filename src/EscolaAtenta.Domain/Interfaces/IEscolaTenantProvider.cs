using System;

namespace EscolaAtenta.Domain.Interfaces;

/// <summary>
/// Provedor isolado de informações do Tenant (Escola).
/// Impede que a camada de Infraestrutura (ex: DbContext) precise 
/// depender diretamente de IConfiguration, atuando como camada de anti-corrupção.
/// </summary>
public interface IEscolaTenantProvider
{
    Guid EscolaId { get; }
}
