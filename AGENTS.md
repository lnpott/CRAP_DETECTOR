# AGENTS.md — CRAP_DETECTOR (Detector de Ato Falho)

App Windows local que registra e correlaciona alterações de janela/foco/UI.
Regras obrigatórias em `CLAUDE.md` (leia antes de codar). Arquitetura e estágios em `docs/PLANO.md`; prompt + critério de aceite de cada etapa em `docs/PROMPTS.md`.
**O código é a fonte de verdade** quando conflitar com os docs.

## Estado real do repo (verificado, não assuma pelo nome dos arquivos)

Isto é scaffolding da **Etapa 1**. Só existem ~155 linhas de código real:
`src/Ato.Core/Observation.cs` (modelos) e `tests/Ato.Core.Tests/MarshallingTests.cs` (2 testes).
`Ato.Storage`, `Ato.Collector`, `Ato.Cli`, `Ato.Storage.Tests`, `Ato.StimulusLab` **não têm nenhum `.cs`**.
Os critérios de aceite da Etapa 1 (esquema SQLite, escritor em lote, replay JSONL) **não estão implementados** — `docs/SPIKES.md` (Etapa 0) e `docs/RELATORIO-ACURACIA.md` (Etapa 5) também não existem.

### `dotnet build CRAP_DETECTOR.sln` falha hoje — 2 causas pré-existentes

1. `src/Ato.Cli` e `src/Ato.Collector` são `OutputType=Exe` sem entry point → `CS5001`.
2. `tests/Ato.Core.Tests/Ato.Core.Tests.csproj` referencia `..\Ato.Core\Ato.Core.csproj` (faltou `..\..\src\`) → `CS0246` em `MarshallingTests.cs`.
   `tests/Ato.Storage.Tests` usa a forma correta `..\..\src\`; copie-a.

Verificado: corrigindo **só** o caminho do `ProjectReference`, `dotnet test tests/Ato.Core.Tests` passa 2/2.
Não atribua esses erros à sua mudança; `Ato.Core` e `Ato.Storage` compilam limpos.

### Outras divergências docs ↔ código

- `docs/PLANO.md` §4 prever `src/Ato.Windows` para os coletores: **não existe** e não está no `.sln`.
- `tests/Ato.StimulusLab/Ato.StimulusLab.csproj` é **library** (sem `OutputType`) e puxa `Microsoft.CSharp` 4.7.0, mas `PLANO.md` §8 descreve o Stimulus Lab como programa que provoca eventos → vai precisar de `OutputType=Exe` + `ProjectReference` para `Ato.Core`.
- `0001-docs-plano.patch` é um patch **já aplicado** (commiteado). Não reaplique.

## Comandos

SDK único instalado: **.NET 8.0.425**. Todos os projetos: `net8.0`, `LangVersion 12.0`, `Nullable=enable`, `ImplicitUsings=enable`.

```bash
dotnet build CRAP_DETECTOR.sln                                  # falha pelas 2 causas acima
dotnet build src/Ato.Core/Ato.Core.csproj                      # projeto único
dotnet test  tests/Ato.Core.Tests/Ato.Core.Tests.csproj
dotnet test  tests/Ato.Core.Tests/Ato.Core.Tests.csproj --filter "FullyQualifiedName~MarshallingTests"
```

Não existe `.editorconfig`, `Directory.Build.props`, `global.json`, CI nem formatter configurado — não invente um; siga o estilo dos arquivos vizinhos.

**Dependências** (`docs/PROMPTS.md`, Etapa 1): só `Microsoft.Data.Sqlite` e o framework de testes. `TraceEvent` só na Etapa 6. Nenhuma outra sem necessidade explícita.

**Testes**: xunit 2.9.3 + `Microsoft.NET.Test.Sdk` 17.12.0 + `xunit.runner.visualstudio` 3.0.2.
`tests/Ato.Core.Tests/Program.cs` é um placeholder só com comentário — o `Microsoft.NET.Test.Sdk` gera o entry point, **não** adicione um `Main` ali.

## Git

Sem `.gitignore` no repo. Só 5 arquivos estão commitados (docs + `CLAUDE.md`/`README.md`); `src/`, `tests/` e o `.sln` estão **untracked**, e `bin/`+`obj/` também aparecem como untracked. Não rode `git add .` — adicionaria artefatos de build.

## Convenções que o código impõe

- **`Observation.Source` = coletor que produziu o evento. `Inference.Origin` = causa inferida.** Mesmo enum `Origin`, significados distintos — é a regra "evento observado ≠ causa inferida" em código. `Observation.Source` começa em `Unknown`, nunca em `User`.
- Enums com casing português (`Confidence.Baixa/Media/Alta`, `Origin.Unknown/User/Application/System/Automation`); identificadores e resto em inglês. Manter.
- `Observation`, `Inference`, `WindowState` são `sealed record` posicionais → imutáveis por construção. Use `with`; não mute.
- Opções canônicas de JSON para fixtures/replay já estão em `tests/Ato.Core.Tests/MarshallingTests.cs:7` (camelCase, `WriteIndented`, `WhenWritingNull`). Reutilize; não crie outro `JsonSerializerOptions`.
- **Identidade de janela = HWND + PID + hora de criação do processo.** HWND sozinho é reutilizado e não identifica janela.
- Callbacks de hook: mínimos (copiar → enfileirar → retornar). Hooks `LL` lentos são removidos silenciosamente pelo Windows → heartbeat obrigatório (Etapa 3).
- `Channel` limitado com **contador de drops**; coalescer `LOCATIONCHANGE` e filtrar `idObject == OBJID_WINDOW` (senão o cursor dispara evento).
- Tempo: QPC local na chegada + tempo da fonte; ordem entre coletores não é garantida → janela de correlação tolerante.
- Teclado/mouse: **só metadados** (classe da tecla, modificadores, flags de injeção, `dwExtraInfo`, botão). Nunca o conteúdo digitado.
- `SendInput` é usado por ferramentas legítimas → injetado ≠ automação maliciosa. Não acuse.

## Fluxo de trabalho

- Uma etapa por vez, colando o prompt de `docs/PROMPTS.md`. Só avance quando o critério de aceite da etapa anterior estiver **validado por execução real**.
- Não implemente o que não foi pedido; não masque erro com tratamento genérico; não afirme que funciona sem evidência.
- Resposta final: o que mudou, arquivos, problemas, validação (comando + saída real), próximo passo.