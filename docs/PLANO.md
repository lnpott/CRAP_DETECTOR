# Detector de Ato Falho — Plano técnico

Status: planejamento. Nenhum código existe ainda; o código é a fonte de verdade assim que surgir.

## 1. Objetivo
Para cada alteração de janela/foco/interface, registrar:
**o que mudou → quando → estado anterior → evento anterior → processo/janela → origem provável → confiança.**

Origens: `USER`, `APPLICATION`, `SYSTEM`, `AUTOMATION`, `UNKNOWN`.
Regra de ouro: **evento observado ≠ causa inferida**. Padrão é `UNKNOWN`. Nunca inventar causalidade.

## 2. O que o Windows permite observar

| Sinal | API | Dá | Não dá |
|---|---|---|---|
| Mudanças de janela | `SetWinEventHook` (`WINEVENT_OUTOFCONTEXT`) | foco, minimizar/restaurar, mover/redimensionar, criar/destruir, mostrar/ocultar, HWND/PID/TID | quem pediu a mudança |
| Entrada | `WH_KEYBOARD_LL`, `WH_MOUSE_LL` | flag de injeção (`LLKHF_INJECTED`, `LLKHF_LOWER_IL_INJECTED`; equivalente `LLMHF_INJECTED` no mouse), `dwExtraInfo` | processo injetor |
| Processos | ETW `Microsoft-Windows-Kernel-Process` | início/fim, PID pai, imagem | causalidade com janela |
| Estado da janela | `GetWindowPlacement`, `DwmGetWindowAttribute`, `QueryFullProcessImageName` | snapshot antes/depois, cloaked, exe | — |

Fatos que moldam o desenho:
- `EVENT_OBJECT_LOCATIONCHANGE` também dispara para o cursor → filtrar `idObject == OBJID_WINDOW` e coalescer.
- `EVENT_SYSTEM_FOREGROUND` não dispara ao restaurar janela minimizada → assinar também `MINIMIZEEND`.
- Sessão ETW em tempo real normalmente exige elevação → ETW é etapa opcional.
- O PID no cabeçalho de um evento ETW é de quem emitiu, não necessariamente de quem causou.
- Raw Input **não** é prova confiável de dispositivo físico (há `hDevice` nulo em entrada legítima) → só evidência secundária, a validar.
- `SendInput` é usado por ferramentas legítimas (acesso remoto, acessibilidade, macros). Injetado ≠ automação maliciosa.

## 3. Stack
- C# / .NET LTS vigente (confirmar versão na Etapa 0), CsWin32 (compile-time), `Microsoft.Data.Sqlite`; `TraceEvent` só na Etapa 6.
- `Core` sem Win32 (portável). Alternativa documentada: Rust.
- Coletor headless + visualizador separado (SQLite somente leitura, WAL). UI: CLI → HTML estático → (opcional) WPF.

## 4. Arquitetura
```
[WinEvent]──┐
[Input LL]──┼─► Channel limitado ─► Enricher+StateTracker ─► Writer (lote) ─► SQLite
[Process]───┘     (drops contados)   (snapshot antes/depois)                    │
                                         Correlator (regras) ─► inferences ─► CLI/HTML
```
```
src/Ato.Core       modelos, StateTracker, regras (sem Win32)
src/Ato.Windows    coletores (P/Invoke via CsWin32)
src/Ato.Storage    SQLite, esquema, retenção
src/Ato.Collector  host
src/Ato.Cli        consulta/relatório
tests/             Core (replay) + Stimulus Lab
```
Regras de captura:
- Callbacks mínimos (copiar, enfileirar, retornar). Hooks LL lentos podem ser removidos pelo sistema → heartbeat.
- Entrada: **somente metadados** (classe da tecla, modificadores, flags, `dwExtraInfo`, botão). Nunca conteúdo digitado.
- Chave de janela: HWND + PID + hora de criação do processo (HWND é reutilizado).
- Tempo: QPC local de chegada + tempo da fonte; ordem entre coletores não é garantida → janela de correlação tolerante.

## 5. Modelo de dados
- `observations` (imutável): `id, ts_qpc, ts_src, source, kind, hwnd, pid, tid, exe, class, title, state_before, state_after, payload`
- `inferences` (derivada, recalculável): `observation_id, origin, confidence(ALTA|MÉDIA|BAIXA), rule_id, rule_version, evidence_ids`
- Confiança categórica, nunca número falsamente preciso.
- SQLite em `%LOCALAPPDATA%`, WAL, `synchronous=NORMAL`, escritor único em lote, retenção por dias/tamanho, sem rede, títulos com opção de truncar/hash.

## 6. Regras de correlação v1
1. Entrada física + coerência de alvo (clique no retângulo, Alt+Tab/Win+tecla físico) → `USER`.
2. Entrada injetada → `UNKNOWN` + `injected_input=true`; `AUTOMATION` só com processo em lista conhecida.
3. Janela mostrada logo após criação do próprio processo, sem entrada → `APPLICATION` (média).
4. Classe/processo do shell (`Shell_TrayWnd`, `Progman`, `WorkerW`) sem entrada → `SYSTEM`.
5. Rajada de `MINIMIZESTART` + foco na área de trabalho → um evento composto; avaliar entrada anterior.
   Cenário-âncora: Win+D físico, Win+D via `SendInput`, clique em "Mostrar área de trabalho".

## 7. Etapas

| # | Entrega | Aceite | Por quê agora |
|---|---|---|---|
| 0 | Spikes descartáveis (WinEvent, hooks LL + `SendInput`, ETW Win32k atribuível?, antivírus/anti-cheat, UIPI/elevação) | Documento "o que é observável nesta máquina" | O SO limita o que o produto pode provar |
| 1 | Core + Storage + harness de replay | Esquema fechado; fixtures JSONL reproduzem inferências | Contrato estável antes da camada instável |
| 2 | Coletor WinEvent + StateTracker + CLI timeline | Minimizar/mover gera evento com antes/depois | Primeiro valor, menor risco |
| 3 | Coletor de entrada (metadados) + heartbeat + Stimulus Lab mínimo | Injetado vs físico rotulado corretamente | Base de `USER` vs `UNKNOWN` |
| 4 | Correlator v1 + agrupamento de rajadas | Cenário Win+D classificado nos 3 casos | Nasce a inferência |
| 5 | Stimulus Lab completo + matriz de erro + baseline CPU/RAM | Relatório de acerto por origem; orçamento medido | Validar antes de somar fontes |
| 6 | Coletor de processos (ETW, opcional/elevado) | Janela ligada a processo e pai | Mais evidência, custo de elevação |
| 7 | Visualizador HTML | Timeline com evidências por inferência | Só após dados confiáveis |
| 8 | Retenção, privacidade, autodiagnóstico, empacotamento | Rodar dias sem crescer sem controle | Endurecimento |

## 8. Testes
- Core: unitário determinístico por replay.
- Stimulus Lab: programa que provoca eventos de origem conhecida (`SendInput` Win+D, `SetForegroundWindow` de outro processo, `ShowWindow`, `MoveWindow`, processo novo com janela) com rótulo-verdade → matriz de confusão. Entrada física = checklist manual.
- Carga: arrastar janela continuamente; medir CPU/RAM/drops. Meta provisória (a medir na Etapa 5): CPU média < 1%, RAM na casa de dezenas de MB.

## 9. Riscos e questionamentos
- Atribuição de origem será frequentemente `UNKNOWN`/baixa: tratar acerto como **métrica**, não requisito.
- "Máxima observabilidade" vs baixo consumo/privacidade: resolvido com metadados, coalescência e retenção.
- "Sem dependências" não vale para ETW (`TraceEvent`): isolado na Etapa 6.
- UIPI: coletor não elevado pode não receber entrada dirigida a janelas elevadas (verificar na Etapa 0).
- Anti-cheat em kernel e antivírus podem restringir/sinalizar hooks (verificar na Etapa 0).
