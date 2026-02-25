// Servico de notificacoes Toast para exibir mensagens ao usuario
// Implementa o padrao Observer: componentes podem se inscrever para receber atualizacoes
namespace EscolaAtenta.WEB.Services;

/// <summary>
/// Modelo de mensagem de toast
/// </summary>
public class ToastMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; } = ToastType.Info;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Interface do servico de toast - permite injecao de dependencias
/// </summary>
public interface IToastService
{
    // Evento que componentes podem assinar para receber novas mensagens
    event Action<ToastMessage>? OnShow;
    
    // Metodos para mostrar diferentes tipos de mensagens
    void ShowInfo(string message);
    void ShowSuccess(string message);
    void ShowWarning(string message);
    void ShowError(string message);
}

/// <summary>
/// Implementacao do servico de toast --dispara eventos para componentes inscritos
/// </summary>
public class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowInfo(string message)
        => Show(message, ToastType.Info);

    public void ShowSuccess(string message)
        => Show(message, ToastType.Success);

    public void ShowWarning(string message)
        => Show(message, ToastType.Warning);

    public void ShowError(string message)
        => Show(message, ToastType.Error);

    private void Show(string message, ToastType type)
    {
        var toast = new ToastMessage
        {
            Message = message,
            Type = type,
            CreatedAt = DateTime.Now
        };
        
        // Dispara o evento - todos os componentes inscritos serao notificados
        OnShow?.Invoke(toast);
    }
}
