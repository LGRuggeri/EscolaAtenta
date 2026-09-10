# Validação dos quatro comentários abertos da PR #7

Revisão verificada: 35ec88bb63abf677d7da423cb29b1c82ca0dadcb, igual ao HEAD remoto da PR consultado nesta validação.

Conclusão: os quatro apontamentos são reais e ainda precisam de correção. Nenhum é apenas uma discussão esquecida depois de corrigida. O código de produção não foi alterado nesta verificação.

| Comentário | Resultado observado | Situação |
|---|---|---|
| P1 — auditoria em gravação de eventos (discussion_r3884438133) | RegistrarPresencaHandler + AppDbContext + LimiteFaltasAtingidoHandler reais criaram um alerta persistido com EscolaId vazio, DataCriacao padrão e UsuarioCriacao nulo. | Reproduzido; corrigir o pré-processamento das gravações em cascata. |
| P1 — transação no despacho (discussion_r3884438140) | Uma exceção sintética no despacho do evento fez a operação falhar, mas o SQLite manteve 1 presença e 0 alertas. Uma nova tentativa foi rejeitada por presença duplicada. | Reproduzido para fluxo sem transação externa; envolver persistência e despacho na mesma transação. |
| P2 — turma rejeitada (discussion_r3884438145) | A função real de recuperação manteve o nome rejeitado, marcou a turma como synced e não consultou o servidor. O delta do SyncPullHandler filtra por DataAtualizacao posterior ao checkpoint; rejeitar uma alteração não atualiza esse campo. | Reproduzido; restaurar os dados autoritativos antes de limpar o estado pendente. |
| P2 — perda de presença na recuperação (discussion_r3884438152) | A função real restaurarPresencaDoServidor chamou destroyPermanently nos três cenários simulados: offline, timeout e HTTP 500. | Reproduzido; preservar a linha quando não houver resposta autoritativa. |

## Como reproduzir

A partir da raiz do repositório:

```powershell
dotnet run --project docs/security-audit/verificacao-pr7/Verificacao.csproj --verbosity quiet
node docs/security-audit/verificacao-pr7/mobile.cjs
```

Os programas retornaram código 0 e `reproduzido: true` para os cenários. São provas do comportamento defeituoso atual, e não testes que certificam uma correção. Depois de corrigir, as expectativas devem ser substituídas por testes de regressão do comportamento seguro.

Backend: SQLite em memória, entidades e handlers reais; dispatcher controlado para encaminhar o evento ao handler real ou lançar uma falha sintética. Chaves estrangeiras desabilitadas somente no banco sintético para dispensar o cadastro do responsável de teste. A falha transacional foi validada sem transação externa, como no caminho normal de RegistrarPresencaHandler.

Mobile: funções privadas reais carregadas via transpileModule, exportadas somente em memória para o ensaio; adaptadores de banco/rede simulados. Não foi executado em aparelho físico. A prova de turma confirma a recuperação incorreta; a permanência após o próximo delta foi conferida no filtro do SyncPullHandler, sem ensaio ponta a ponta.

Os 285 testes anteriores não descartavam esses defeitos: o FakeMediator usual ignora os eventos, e os quatro testes mobile cobriam isolamento de sessão/banco, não estes caminhos de recuperação.

Nenhum comentário da PR foi encerrado, nenhum commit ou push foi feito nesta validação. As mesmas falhas permanecem nos pacotes 1.4.2 gerados da revisão verificada.

## Estado após correção

Este documento acima descreve a reprodução na revisão 35ec88b, não o estado corrigido.
As expectativas seguras estão agora em `Tests/EscolaAtenta.Application.Tests/Security/CascataPersistenciaTests.cs` e `src/EscolaAtenta.App/tests/syncRecovery.test.cjs`. As reproduções históricas daqui esperam o defeito; sua falha após o patch é esperada. Consulte `../correcoes.md` para as mudanças.
