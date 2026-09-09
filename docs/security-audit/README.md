# Auditoria de segurança - Escola Atenta

Revisão auditada: f7550204891aa6f2914a9280fe26ddb2d55ef793 (estado inicial limpo).

## Entregáveis

- relatorio-auditoria-seguranca.pdf: relatório final em pt-BR.
- dados.json: achados, evidências, condições, recomendações e cobertura.
- cobertura.md: inventário completo dos handlers e rotas, proteções e limitações.
- issues.md: oito rascunhos de issues; não publicados.
- reproducoes.json: resultados de 15 cenários e controles locais.
- inventario.json: inventário de arquivos/hashes e busca de segredos no histórico, sem valores sensíveis.
- bundle.json: inspeção limitada do sourcemap disponível.
- verificacao-pdf.json: contagens e verificação de extração do PDF.

## Regenerar o relatório (sem reinvestigar)

Python 3.11+, reportlab 4+, pypdf 5+. Não exige matplotlib, recursos remotos ou caminhos absolutos.

```sh
python docs/security-audit/gerar_relatorio.py
```

Os arquivos em fontes/ são Bitstream Vera distribuídos pelo ReportLab; licença em fontes/LICENSE.txt.

## Reproduzir os casos de segurança

```sh
dotnet run --project docs/security-audit/repro/Auditoria.csproj
dotnet test --nologo --verbosity quiet
```

Requer .NET 9 e dependências NuGet do projeto. O host usa SQLite em memória, usuários sintéticos e porta loopback efêmera. Não inicia o serviço Windows, não toca no banco real e não imprime tokens ou senhas. Usa controllers e handlers reais; omite rate limiter, workers, startup e middleware global de erros. A prova do fallback JWT é criptográfica local e condicional a Development. Não equivale a teste end-to-end do Program.cs.

## Scripts auxiliares

- consolidar.py: mantém o texto editorial e reconstrói dados.json, cobertura.md e issues.md a partir da revisão local; para regenerar apenas o PDF, não é necessário executá-lo.
- inventariar.py: nova busca heurística de arquivos textuais e histórico Git local; pode demorar e não substitui revisão manual.
- gerar_relatorio.py: usa exclusivamente os dados/fontes locais.

Segredos redigidos. Nenhuma credencial foi testada externamente. Os 267 testes existentes passaram; não provam ausência de vulnerabilidades. EA-02 tem verificação estática e EA-08 é condicional ao deploy, sem teste Android/ACL real. APKs e Hermes não foram decompilados.

Referência para EA-08: https://jrsoftware.org/ishelp/topic_dirssection.htm (readexec permite leitura dos arquivos da pasta e subpastas; consultado em 08/09/2026).
