namespace EscolaAtenta.API.Middleware;

/// <summary>
/// Middleware de Security Headers — Defense in Depth.
/// 
/// Injeta cabeçalhos de segurança em TODAS as respostas HTTP:
/// - X-Content-Type-Options: nosniff → impede MIME-type sniffing
/// - X-Frame-Options: DENY → impede clickjacking via iframe
/// - X-XSS-Protection: 1; mode=block → proteção contra XSS refletido
/// - Referrer-Policy → limita vazamento de URLs em referers
/// - Permissions-Policy → bloqueia acesso a câmera, microfone, geolocalização
/// 
/// Remove cabeçalhos que expõem infraestrutura:
/// - X-Powered-By → removido explicitamente
/// - Server → suprimido via Kestrel config (AddServerHeader = false)
/// 
/// Posicionamento no pipeline: APÓS UseExceptionHandler, ANTES de UseSerilogRequestLogging.
/// Isso garante que mesmo respostas de erro incluam os headers de segurança.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Injeta headers de segurança ANTES de processar a requisição
        // para garantir presença mesmo em respostas de erro
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Impede MIME-type sniffing — browsers devem respeitar Content-Type declarado
            headers.Append("X-Content-Type-Options", "nosniff");

            // Impede que a página seja carregada em um iframe (proteção contra clickjacking)
            headers.Append("X-Frame-Options", "DENY");

            // Ativa filtro XSS do browser (proteção contra XSS refletido)
            headers.Append("X-XSS-Protection", "1; mode=block");

            // Limita informações enviadas via Referer header
            headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            // Bloqueia acesso a funcionalidades sensíveis do dispositivo
            headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

            // Remove cabeçalhos que expõem detalhes da infraestrutura
            headers.Remove("X-Powered-By");
            headers.Remove("Server");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
