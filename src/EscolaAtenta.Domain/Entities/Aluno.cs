using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Representa um aluno matriculado no sistema.
/// 
/// Invariantes protegidas:
/// 1. Nome e Matrícula são obrigatórios e têm tamanho máximo.
/// 2. Um aluno deve estar associado a uma turma válida.
/// 3. Alunos não podem ser excluídos fisicamente (ISoftDeletable).
/// 4. A verificação de limite de faltas dispara Domain Event para geração de alerta.
/// 
/// Novas Regras de Negócio (Pivot):
/// - O sistema é operado por um Monitor que passa de sala em sala.
/// - O foco é alertar a Supervisão sobre faltas consecutivas.
/// - Alerta de evasão é gerado APENAS quando FaltasConsecutivasAtuais == 3.
/// 
/// Decisão sobre Soft Delete: Alunos são dados históricos críticos.
/// Mesmo após desligamento, seus registros de presença devem ser preservados
/// para fins de auditoria e relatórios históricos.
/// </summary>
public class Aluno : EntityBase, ISoftDeletable
{
    private readonly List<RegistroPresenca> _registrosPresenca = [];
    private readonly List<AlertaEvasao> _alertasEvasao = [];

    // Construtor privado para uso exclusivo do EF Core
    private Aluno() { }

    /// <summary>
    /// Cria um novo aluno validando todas as invariantes.
    /// </summary>
    public Aluno(Guid id, string nome, string? matricula, Guid turmaId)
        : base(id)
    {
        ValidarNome(nome);

        if (turmaId == Guid.Empty)
            throw new DomainException("O aluno deve estar associado a uma turma válida.");

        Nome = nome;
        Matricula = matricula?.Trim() ?? string.Empty;
        TurmaId = turmaId;
        Ativo = true; // Todo aluno nasce ativo
        FaltasConsecutivasAtuais = 0; // Inicializa contadores de falta
        TotalFaltas = 0;
    }

    public string Nome { get; private set; } = string.Empty;
    public string Matricula { get; private set; } = string.Empty;
    public Guid TurmaId { get; private set; }

    // ── Controle de Faltas (Novas propriedades) ───────────────────────────────
    /// <summary>
    /// Número de faltas consecutivas atuais.
    /// Zera quando o aluno comparece (Presente).
    /// </summary>
    public int FaltasConsecutivasAtuais { get; private set; }

    /// <summary>
    /// Total de faltas acumuladas na história do aluno.
    /// </summary>
    public int TotalFaltas { get; private set; }

    // ── Resumo do Trimestre (Novas regras) ─────────────────────────────────────
    public int AtrasosNoTrimestre { get; private set; }
    public int FaltasNoTrimestre { get; private set; }
    public DateTime DataInicioTrimestre { get; private set; } = DateTime.UtcNow;

    // ── ISoftDeletable ─────────────────────────────────────────────────────────
    public bool Ativo { get; private set; }
    public DateTimeOffset? DataExclusao { get; private set; }
    public string? UsuarioExclusao { get; private set; }

    // ── Navegação ──────────────────────────────────────────────────────────────
    public virtual Turma Turma { get; private set; } = null!;

    public IReadOnlyCollection<RegistroPresenca> RegistrosPresenca =>
        _registrosPresenca.AsReadOnly();

    public IReadOnlyCollection<AlertaEvasao> AlertasEvasao =>
        _alertasEvasao.AsReadOnly();

    // ── Métodos de Negócio ─────────────────────────────────────────────────────

    /// <summary>
    /// Atualiza os dados cadastrais do aluno.
    /// </summary>
    public void Atualizar(string nome, string? matricula)
    {
        ValidarNome(nome);
        Nome = nome;
        Matricula = matricula?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Transfere o aluno para outra turma.
    /// </summary>
    public void TransferirTurma(Guid novaTurmaId)
    {
        if (novaTurmaId == Guid.Empty)
            throw new DomainException("A turma de destino deve ser válida.");

        if (novaTurmaId == TurmaId)
            throw new DomainException("O aluno já pertence a esta turma.");

        TurmaId = novaTurmaId;
    }

    public void VerificarEReiniciarCicloTrimestral(DateTime dataAtual)
    {
        if ((dataAtual - DataInicioTrimestre).TotalDays >= 90)
        {
            AtrasosNoTrimestre = 0;
            FaltasNoTrimestre = 0;
            FaltasConsecutivasAtuais = 0; // Limpa o ciclo
            DataInicioTrimestre = dataAtual;
        }
    }

    public void RegistrarAtraso(DateTime dataAtual)
    {
        VerificarEReiniciarCicloTrimestral(dataAtual);
        AtrasosNoTrimestre++;

        // O Domínio se auto-protege: reage à própria mutação imediatamente.
        // Os Handlers não precisam mais injetar lógica de alerta para atrasos.
        VerificarLimiteAtrasos();
    }

    public void RegistrarFalta(DateTime dataAtual)
    {
        VerificarEReiniciarCicloTrimestral(dataAtual);
        FaltasNoTrimestre++;
        TotalFaltas++;
        FaltasConsecutivasAtuais++;

        // O Dominício se auto-protege: reage à própria mutação imediatamente.
        // Os Handlers não precisam mais chamar VerificarLimiteFaltas() explicitamente.
        VerificarLimiteFaltas();
    }

    public void RegistrarPresenca(DateTime dataAtual)
    {
        VerificarEReiniciarCicloTrimestral(dataAtual);
        FaltasConsecutivasAtuais = 0; // Presença quebra a sequência de faltas
    }

    /// <summary>
    /// Mantido por compatibilidade com handlers existentes.
    /// Delega para as novas regras de negócio baseando-se na data atual.
    /// </summary>
    public void RegistrarPresenca(StatusPresenca status, DateTime dataAtual)
    {
        switch (status)
        {
            case StatusPresenca.Presente:
                RegistrarPresenca(dataAtual);
                break;
            case StatusPresenca.Falta:
            case StatusPresenca.Ausente:
                RegistrarFalta(dataAtual);
                break;
            case StatusPresenca.FaltaJustificada:
                RegistrarPresenca(dataAtual); // Falta justificada zera consecutivas
                TotalFaltas++; // Mas conta no total histórico
                break;
            case StatusPresenca.Atraso:
                RegistrarAtraso(dataAtual);
                break;
        }
    }

    public void VerificarLimiteFaltas()
    {
        // Conforme a nova regra, gerar alertas com severidades crescentes:
        // 1 - Aviso (Amarelo)
        // 2 - Intermediário (Laranja -> Conversa com o aluno)
        // 3 - Vermelho (Conversa com os pais)
        // 5 - Preto (Conselho Tutelar)
        //
        // Thresholds explícitos: evita falhas silenciosas por saltos no contador
        // (ex: edições em lote que pulam de 2 para 4 diretamente).
        if (FaltasConsecutivasAtuais == 1 || FaltasConsecutivasAtuais == 2 || 
            FaltasConsecutivasAtuais == 3 || FaltasConsecutivasAtuais == 5)
        {
            var nivelAlerta = GetNivelAlerta();
            
            AddDomainEvent(new LimiteFaltasAtingidoEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                TotalFaltas: FaltasConsecutivasAtuais,
                LimiteConfigurado: 5, // Teto configurado do conselho tutelar
                MotivoExato: $"O aluno alcançou {FaltasConsecutivasAtuais} falhas consecutivas.",
                Nivel: nivelAlerta
            ));
        }
    }

    /// <summary>
    /// Verifica se o aluno atingiu um limiar de atrasos no trimestre e dispara
    /// o Domain Event correspondente para criação do alerta.
    /// 
    /// Thresholds explícitos (evita falhas silenciosas por saltos de contador):
    /// - 3 atrasos → Aviso (comunicar ao aluno)
    /// - 6 atrasos → Intermediário (comunicar aos pais)
    /// </summary>
    public void VerificarLimiteAtrasos()
    {
        if (AtrasosNoTrimestre == 3 || AtrasosNoTrimestre == 6)
        {
            var nivel = AtrasosNoTrimestre >= 6
                ? NivelAlertaFalta.Intermediario
                : NivelAlertaFalta.Aviso;

            AddDomainEvent(new LimiteAtrasosAtingidoEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                TotalAtrasos: AtrasosNoTrimestre,
                MotivoExato: $"O aluno acumulou {AtrasosNoTrimestre} atrasos no trimestre.",
                Nivel: nivel
            ));
        }
    }

    /// <summary>
    /// Retorna o nível de alerta baseado nas faltas consecutivas.
    /// Utiliza a extensão do enum para garantir consistência e limites.
    /// </summary>
    public NivelAlertaFalta GetNivelAlerta()
    {
        // Usa a factory method do enum que garante o limite máximo (Preto = 5)
        return NivelAlertaFaltaExtensions.DeFaltasConsecutivas(FaltasConsecutivasAtuais);
    }

    /// <summary>
    /// Realiza a exclusão lógica do aluno.
    /// O aluno não pode ser excluído fisicamente — apenas desativado.
    /// </summary>
    /// <param name="usuarioExclusao">Identificador do usuário que realizou a exclusão.</param>
    public void Desativar(string usuarioExclusao)
    {
        if (!Ativo)
            throw new DomainException("O aluno já está inativo.");

        Ativo = false;
        DataExclusao = DateTimeOffset.UtcNow;
        UsuarioExclusao = usuarioExclusao;
    }

    // ── Validações Privadas ────────────────────────────────────────────────────

    private static void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do aluno é obrigatório.");

        if (nome.Length > 200)
            throw new DomainException("O nome do aluno não pode ter mais de 200 caracteres.");

        // Bloqueia caracteres de controle (ex: \0, \r, \n) que podem causar injeção ou corrupção
        if (nome.Any(c => char.IsControl(c)))
            throw new DomainException("O nome do aluno contém caracteres inválidos.");
    }

    // Matrícula é opcional — validação removida por decisão de negócio
}
