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
    private readonly List<AlunoTurmaHistorico> _historicoTurmas = [];

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

    public IReadOnlyCollection<AlunoTurmaHistorico> HistoricoTurmas =>
        _historicoTurmas.AsReadOnly();

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
    /// Cria a primeira matrícula do aluno na turma informada.
    /// Deve ser chamado pelo handler logo após a criação do aluno.
    /// </summary>
    public void Matricular(Guid turmaId, int anoLetivo, DateTime dataInicio, string? motivo)
    {
        if (turmaId == Guid.Empty)
            throw new DomainException("A turma de matrícula deve ser válida.");

        if (_historicoTurmas.Any(h => h.Ativa))
            throw new DomainException("O aluno já possui uma matrícula ativa.");

        var matricula = new AlunoTurmaHistorico(
            Guid.NewGuid(),
            Id,
            turmaId,
            anoLetivo,
            dataInicio,
            dataFim: null,
            motivo);

        _historicoTurmas.Add(matricula);
    }

    /// <summary>
    /// Transfere o aluno para outra turma, encerrando a matrícula ativa e
    /// retornando a nova matrícula para que o repositório/camada de aplicação
    /// possa persisti-la.
    /// </summary>
    public AlunoTurmaHistorico TransferirTurma(Guid novaTurmaId, int anoLetivoDestino, DateTime dataTransferencia, string? motivo)
    {
        if (novaTurmaId == Guid.Empty)
            throw new DomainException("A turma de destino deve ser válida.");

        if (novaTurmaId == TurmaId)
            throw new DomainException("O aluno já pertence a esta turma.");

        var matriculaAtiva = _historicoTurmas.FirstOrDefault(h => h.Ativa);
        if (matriculaAtiva != null)
        {
            matriculaAtiva.Encerrar(dataTransferencia);
        }

        TurmaId = novaTurmaId;

        var novaMatricula = new AlunoTurmaHistorico(
            Guid.NewGuid(),
            Id,
            novaTurmaId,
            anoLetivoDestino,
            dataTransferencia,
            dataFim: null,
            motivo);

        // Nota: a nova matrícula não é adicionada à coleção de navegação aqui
        // porque, em alguns providers EF Core (InMemory/SQLite), a detecção de
        // mudanças em coleções privadas com backing field após o primeiro
        // SaveChanges pode falhar. O handler/adicionador persiste a entidade
        // diretamente no contexto.

        AddDomainEvent(new AlunoTransferidoEvent(
            Id,
            Nome,
            matriculaAtiva?.TurmaId ?? Guid.Empty,
            novaTurmaId,
            dataTransferencia,
            motivo));

        return novaMatricula;
    }

    /// <summary>
    /// Retorna a matrícula ativa do aluno, se houver.
    /// </summary>
    public AlunoTurmaHistorico? ObterMatriculaAtiva()
    {
        return _historicoTurmas.FirstOrDefault(h => h.Ativa);
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
        RegistrarAtraso(dataAtual, dispararEventos: true);
    }

    private void RegistrarAtraso(DateTime dataAtual, bool dispararEventos)
    {
        RegistrarAtraso(dataAtual, dispararEventos, verificarCiclo: true);
    }

    private void RegistrarAtraso(DateTime dataAtual, bool dispararEventos, bool verificarCiclo)
    {
        if (verificarCiclo)
            VerificarEReiniciarCicloTrimestral(dataAtual);

        AtrasosNoTrimestre++;

        if (dispararEventos)
            VerificarLimiteAtrasos();
    }

    public void RegistrarFalta(DateTime dataAtual)
    {
        RegistrarFalta(dataAtual, dispararEventos: true);
    }

    private void RegistrarFalta(DateTime dataAtual, bool dispararEventos)
    {
        RegistrarFalta(dataAtual, dispararEventos, verificarCiclo: true);
    }

    private void RegistrarFalta(DateTime dataAtual, bool dispararEventos, bool verificarCiclo)
    {
        if (verificarCiclo)
            VerificarEReiniciarCicloTrimestral(dataAtual);

        FaltasNoTrimestre++;
        TotalFaltas++;
        FaltasConsecutivasAtuais++;

        if (dispararEventos)
            VerificarLimiteFaltas();
    }

    public void RegistrarPresenca(DateTime dataAtual)
    {
        RegistrarPresenca(dataAtual, dispararEventos: true);
    }

    private void RegistrarPresenca(DateTime dataAtual, bool dispararEventos)
    {
        RegistrarPresenca(dataAtual, dispararEventos, verificarCiclo: true);
    }

    private void RegistrarPresenca(DateTime dataAtual, bool dispararEventos, bool verificarCiclo)
    {
        if (verificarCiclo)
            VerificarEReiniciarCicloTrimestral(dataAtual);

        FaltasConsecutivasAtuais = 0; // Presença quebra a sequência de faltas
    }

    /// <summary>
    /// Mantido por compatibilidade com handlers existentes.
    /// Delega para as novas regras de negócio baseando-se na data atual.
    /// </summary>
    public void RegistrarPresenca(StatusPresenca status, DateTime dataAtual)
    {
        RegistrarPresenca(status, dataAtual, dispararEventos: true);
    }

    private void RegistrarPresenca(StatusPresenca status, DateTime dataAtual, bool dispararEventos)
    {
        RegistrarPresenca(status, dataAtual, dispararEventos, verificarCiclo: true);
    }

    private void RegistrarPresenca(StatusPresenca status, DateTime dataAtual, bool dispararEventos, bool verificarCiclo)
    {
        switch (status)
        {
            case StatusPresenca.Presente:
                RegistrarPresenca(dataAtual, dispararEventos, verificarCiclo);
                break;
            case StatusPresenca.Falta:
            case StatusPresenca.Ausente:
                RegistrarFalta(dataAtual, dispararEventos, verificarCiclo);
                break;
            case StatusPresenca.FaltaJustificada:
                RegistrarPresenca(dataAtual, dispararEventos, verificarCiclo); // Falta justificada zera consecutivas
                TotalFaltas++; // Mas conta no total histórico
                break;
            case StatusPresenca.Atraso:
                RegistrarAtraso(dataAtual, dispararEventos, verificarCiclo);
                break;
        }
    }

    /// <summary>
    /// Recalcula todas as estatísticas do aluno a partir do histórico de presenças.
    /// Limpa eventos pendentes para evitar duplicar alertas quando o recálculo
    /// é executado após outras operações do mesmo batch já terem enfileirado eventos.
    ///
    /// Importante: este método NÃO dispara eventos de threshold. A emissão/escalação/
    /// resolução de alertas deve ser feita por <see cref="ReconciliarAlertasPendentes"/>,
    /// garantindo uma única fonte de decisão sobre alertas e evitando eventos duplicados.
    ///
    /// Também preserva o início do ciclo trimestral ativo e restringe o replay:
    /// - TotalFaltas: conta o histórico completo.
    /// - FaltasNoTrimestre, AtrasosNoTrimestre e FaltasConsecutivasAtuais: contam
    ///   apenas registros dentro do ciclo trimestral atual (≥ DataInicioTrimestre).
    /// </summary>
    public void RecalcularEstatisticas(IEnumerable<RegistroPresenca> historico)
    {
        if (historico == null)
            throw new DomainException("O histórico de presenças não pode ser nulo.");

        // Evita que eventos de domínio previamente enfileirados sejam publicados em duplicata
        ClearDomainEvents();

        var ordenado = historico
            .Where(r => r.Chamada != null)
            .OrderBy(r => r.Chamada.DataHora)
            .ToList();

        // Salva o ciclo trimestral atual para não mover a fronteira durante o recálculo
        var cicloAtual = DataInicioTrimestre;
        var dataReferencia = ordenado.LastOrDefault()?.Chamada.DataHora.UtcDateTime ?? DateTime.UtcNow;

        // O ciclo está ativo apenas se a data mais recente do histórico pertence
        // ao ciclo atual (dataReferencia >= cicloAtual) e está dentro da janela de 90 dias.
        var cicloEstaAtivo = cicloAtual != default
            && dataReferencia >= cicloAtual
            && (dataReferencia - cicloAtual).TotalDays < 90;

        // Se o ciclo expirou, reconstrói os limites cronologicamente a partir dos
        // resets de 90 dias. Isso evita que uma janela deslizante puxe registros de
        // ciclos anteriores para o trimestre atual ao recalcular.
        if (!cicloEstaAtivo && ordenado.Count > 0)
        {
            var datas = ordenado
                .Select(r => r.Chamada.DataHora.UtcDateTime)
                .ToList();

            var inicioReconstruido = datas.First();
            foreach (var data in datas)
            {
                if ((data - inicioReconstruido).TotalDays >= 90)
                {
                    inicioReconstruido = data;
                }
            }

            DataInicioTrimestre = inicioReconstruido;
        }
        else if (!cicloEstaAtivo)
        {
            // Sem histórico: usa a data atual
            DataInicioTrimestre = DateTime.UtcNow;
        }

        var ciclo = DataInicioTrimestre;

        // Reseta os contadores para recomputar do zero
        TotalFaltas = 0;
        FaltasConsecutivasAtuais = 0;
        FaltasNoTrimestre = 0;
        AtrasosNoTrimestre = 0;

        foreach (var registro in ordenado)
        {
            var dataRegistro = registro.Chamada.DataHora.UtcDateTime;

            // TotalFaltas: conta o histórico completo, independentemente do ciclo
            if (registro.Status is StatusPresenca.Falta or StatusPresenca.Ausente or StatusPresenca.FaltaJustificada)
                TotalFaltas++;

            // Contadores trimestrais e consecutivos: apenas dentro do ciclo ativo
            if (dataRegistro < ciclo)
                continue;

            switch (registro.Status)
            {
                case StatusPresenca.Falta:
                case StatusPresenca.Ausente:
                    FaltasNoTrimestre++;
                    FaltasConsecutivasAtuais++;
                    break;
                case StatusPresenca.Atraso:
                    AtrasosNoTrimestre++;
                    break;
                case StatusPresenca.FaltaJustificada:
                    // Falta justificada quebra a sequência de faltas, mas não conta como falta no trimestre
                    FaltasConsecutivasAtuais = 0;
                    break;
                case StatusPresenca.Presente:
                    FaltasConsecutivasAtuais = 0;
                    break;
            }
        }
    }

    /// <summary>
    /// Reconcilia alertas pendentes com os contadores finais do aluno após
    /// recálculo do histórico. Rebaixa o nível quando o contador cai para um
    /// threshold inferior, resolve quando cai abaixo de todos os thresholds e
    /// escala/cria quando atinge um threshold.
    /// </summary>
    public void ReconciliarAlertasPendentes()
    {
        if (FaltasConsecutivasAtuais > 0)
        {
            AddDomainEvent(new LimiteFaltasAtingidoEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                TotalFaltas: FaltasConsecutivasAtuais,
                LimiteConfigurado: 5,
                MotivoExato: $"O aluno alcançou {FaltasConsecutivasAtuais} falhas consecutivas.",
                Nivel: GetNivelAlerta()
            ));
        }
        else
        {
            AddDomainEvent(new FaltasConsecutivasNormalizadasEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                FaltasConsecutivasAtuais: FaltasConsecutivasAtuais
            ));
        }

        if (AtrasosNoTrimestre >= 3)
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
        else
        {
            AddDomainEvent(new AtrasosTrimestreNormalizadosEvent(
                AlunoId: Id,
                TurmaId: TurmaId,
                NomeAluno: Nome,
                AtrasosNoTrimestre: AtrasosNoTrimestre
            ));
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
