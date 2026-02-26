using System.Security.Cryptography;
using System.Text;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>
/// Classe utilitária para geração de senhas iniciais seguras.
/// Útil para a criação do primeiro Administrador e novos usuários.
/// </summary>
public static class PasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Numerics = "0123456789";
    private const string Specials = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    /// <summary>
    /// Gera uma senha aleatória contendo pelo menos 1 maiúscula, 1 minúscula, 1 número e 1 caractere especial.
    /// </summary>
    /// <param name="length">Tamanho da senha (padrão 12)</param>
    /// <returns>Senha em texto plano gerada</returns>
    public static string Generate(int length = 12)
    {
        if (length < 8)
            throw new ArgumentOutOfRangeException(nameof(length), "A senha deve ter pelo menos 8 caracteres.");

        var charSet = Lowercase + Uppercase + Numerics + Specials;
        var password = new char[length];
        
        // Garante a existência de pelo menos um de cada tipo
        password[0] = Lowercase[RandomNumberGenerator.GetInt32(Lowercase.Length)];
        password[1] = Uppercase[RandomNumberGenerator.GetInt32(Uppercase.Length)];
        password[2] = Numerics[RandomNumberGenerator.GetInt32(Numerics.Length)];
        password[3] = Specials[RandomNumberGenerator.GetInt32(Specials.Length)];

        // Preenche o restante aleatoriamente
        for (int i = 4; i < length; i++)
        {
            password[i] = charSet[RandomNumberGenerator.GetInt32(charSet.Length)];
        }

        // Embaralha para que os primeiros caracteres não sejam previsíveis
        RandomNumberGenerator.Shuffle(password.AsSpan());

        return new string(password);
    }
}
