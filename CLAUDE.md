# Escola Atenta — Instruções para Agentes

## Knowledge Base

Este projeto usa o Agent Knowledge Base (agent_kb) como fonte centralizada de contexto.
**Não crie arquivos .md de documentação ou contexto.** Use o CLI para consultar e gravar conhecimento.

### Antes de iniciar qualquer tarefa

```bash
python "c:/Users/RUGGERI FAMILY/DB LLM/agent_kb.py" context-pack "<descricao da tarefa>" --project-id "83d61dbd-2632-4216-be24-633186c9fe8e" --budget 4000
```

### Buscar informação específica

```bash
python "c:/Users/RUGGERI FAMILY/DB LLM/agent_kb.py" search "<termo>" --project-id "83d61dbd-2632-4216-be24-633186c9fe8e"
```

### Ao aprender algo novo ou tomar decisão

```bash
python "c:/Users/RUGGERI FAMILY/DB LLM/agent_kb.py" memory add \
  --title "..." --summary "..." --content "..." \
  --node-type ADR \
  --importance high \
  --project-id "83d61dbd-2632-4216-be24-633186c9fe8e"
```

### Referência rápida

- **API**: http://0.0.0.0:5114 | Health: /health
- **Project ID**: `83d61dbd-2632-4216-be24-633186c9fe8e`
- **Stack**: ASP.NET Core 9, SQLite WAL, EF Core 9, MediatR CQRS, DDD, React Native Expo
- **Arquitetura**: Clean Architecture DDD (Domain → Application → Infrastructure → API)
- **Multi-tenant**: EscolaId (auto-set no AppDbContext via IEscolaTenantProvider)
- **Soft Delete**: ISoftDeletable + Global Query Filters (nunca deletar fisicamente)
- **Domain Events**: MediatR, despacho síncrono pós-SaveChangesAsync
- **Responder sempre em PT-BR**

### Regras obrigatórias

1. Consultar `context-pack` antes de iniciar tarefas
2. Nunca gerar arquivos .md de contexto — usar `memory add` no agent_kb
3. Citar IDs (rule:X, mem:Y) ao justificar decisões arquiteturais
4. Rich Domain Model: lógica de negócio nas entities, private setters, Domain Events
5. Toda entity herda de EntityBase (audit fields + EscolaId + DomainEvents)
6. Soft Delete obrigatório para Aluno, Turma, Usuario (ISoftDeletable)
7. Commands e Queries via MediatR handlers (nunca lógica em controllers)
