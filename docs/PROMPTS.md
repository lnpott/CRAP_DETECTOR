# Prompts por etapa

Uso: cole um prompt por vez no agente de desenvolvimento. Só avance quando o critério de aceite da etapa anterior estiver validado.
Todos assumem `CLAUDE.md` e `docs/PLANO.md` como contexto.

---
## Etapa 0 — Spikes de viabilidade (descartável)
```
Execute a Etapa 0 de docs/PLANO.md. Crie projetos de console descartáveis em spikes/ (não fazem parte da solução final).
Verifique nesta máquina, com evidência (logs/saídas), e registre em docs/SPIKES.md:
(a) SetWinEventHook (WINEVENT_OUTOFCONTEXT): FOREGROUND, MINIMIZESTART/END, MOVESIZESTART/END, OBJECT_SHOW/HIDE, LOCATIONCHANGE (filtrar OBJID_WINDOW). Meça eventos/s em repouso e arrastando janela.
(b) Hooks WH_KEYBOARD_LL/WH_MOUSE_LL: registrar apenas metadados (flags de injeção, dwExtraInfo, modificadores). Gere Win+D com SendInput e compare com físico (manual).
(c) Se algum provider ETW (ex.: Microsoft-Windows-Win32k) expõe algo atribuível a SendInput/foco. Conclua go/no-go.
(d) Comportamento com janela elevada (UIPI) e com antivírus/anti-cheat ativos.
(e) Versão LTS do .NET a usar.
Não escreva código de produção. Conclua com: o que é observável, o que não é, e ajustes recomendados ao plano.
```
Aceite: docs/SPIKES.md com evidências e decisões (stack, ETW go/no-go, elevação).

---
## Etapa 1 — Core, Storage e replay
```
Execute a Etapa 1. Crie a solução .NET com src/Ato.Core, src/Ato.Storage e tests/Ato.Core.Tests, sem Win32.
Implemente: modelos Observation e Inference conforme docs/PLANO.md §5 (origem e confiança como enums), esquema SQLite (WAL), escritor único em lote, e um leitor de fixtures JSONL que alimenta o pipeline (replay).
Dependências permitidas: Microsoft.Data.Sqlite e framework de testes. Nada mais.
Aceite: testes passam; uma fixture JSONL é gravada e relida idêntica; observations é imutável por construção.
Valide executando os testes e informe o resultado real.
```

---
## Etapa 2 — Coletor WinEvent + StateTracker
```
Execute a Etapa 2. Crie src/Ato.Windows (CsWin32) e src/Ato.Cli.
Implemente o coletor SetWinEventHook em thread própria com message loop, callbacks mínimos, Channel limitado com contador de descartes, filtro OBJID_WINDOW, coalescência de LOCATIONCHANGE, e assinatura de MINIMIZEEND.
Implemente o StateTracker (chave HWND+PID+hora de criação do processo) que produz state_before/state_after. Título com cuidado para não bloquear em janela travada.
CLI: `ato timeline` lista observações recentes.
Aceite: minimizar e mover uma janela gera observações com estado antes/depois, verificado em execução real. Reporte eventos/s e drops medidos.
```

---
## Etapa 3 — Entrada (metadados) + heartbeat + Stimulus Lab mínimo
```
Execute a Etapa 3. Implemente coletores WH_KEYBOARD_LL e WH_MOUSE_LL registrando SOMENTE metadados (classe da tecla, modificadores, flags de injeção incl. lower-IL, dwExtraInfo, botão). Jamais conteúdo digitado.
Callback mínimo (copiar e enfileirar). Adicione heartbeat que detecte remoção silenciosa do hook.
Crie tests/Ato.StimulusLab com um caso: SendInput de Win+D (injetado). Registre também o procedimento manual para Win+D físico.
Aceite: injetado e físico aparecem rotulados corretamente nas observações; heartbeat sinaliza hook perdido (simule).
```

---
## Etapa 4 — Correlator v1
```
Execute a Etapa 4. Em Ato.Core, implemente o Correlator com as regras de docs/PLANO.md §6, cada uma com rule_id e rule_version, citando evidence_ids. Padrão UNKNOWN. Confiança categórica.
Implemente agrupamento de rajadas (minimizar-todos → evento composto).
Escreva fixtures de replay para: Win+D físico, Win+D injetado, clique em "Mostrar área de trabalho", mudança sem entrada.
Aceite: as 4 fixtures produzem a origem/confiança esperadas, com testes automatizados. Não afirme causalidade além das regras.
```

---
## Etapa 5 — Stimulus Lab completo e baseline
```
Execute a Etapa 5. Expanda o Stimulus Lab com casos de origem conhecida: SendInput Win+D, SetForegroundWindow vindo de outro processo, ShowWindow, MoveWindow, processo novo com janela. Cada caso com rótulo-verdade.
Gere relatório com matriz de confusão por origem e acerto por regra. Meça CPU/RAM/drops em repouso e sob arraste contínuo de janela.
Aceite: relatório em docs/RELATORIO-ACURACIA.md com números medidos; liste falhas das regras sem ocultá-las.
```

---
## Etapa 6 — Processos (ETW, opcional)
```
Execute a Etapa 6 somente se a Etapa 0 deu go. Adicione coletor de Microsoft-Windows-Kernel-Process via TraceEvent (única nova dependência), como componente opcional que exige elevação e degrada com elegância sem ela.
Registre PID pai, imagem e hora de início; ligue janela→processo→pai no StateTracker. Não trate o PID do cabeçalho ETW como causa.
Aceite: janela de app recém-iniciado aparece ligada ao processo e ao pai; sem elevação, o coletor desativa e registra isso.
```

---
## Etapa 7 — Visualizador
```
Execute a Etapa 7. Gere timeline HTML estática a partir do SQLite (somente leitura), sem servidor. Cada inferência mostra origem, confiança, regra e evidências; observações e inferências visualmente separadas.
Aceite: abrir o HTML mostra o cenário Win+D com evidências navegáveis.
```

---
## Etapa 8 — Endurecimento
```
Execute a Etapa 8. Retenção por dias/tamanho, truncar/hash opcional de títulos, autodiagnóstico (drops, latência de hook, heartbeat), empacotamento simples.
Aceite: execução contínua por vários dias sem crescimento descontrolado de disco/memória, medida e reportada.
```
