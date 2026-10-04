# Detector de Ato Falho — instruções para agentes

Fonte de verdade: o código existente. Plano em `docs/PLANO.md`; prompts por etapa em `docs/PROMPTS.md`.

## Regras
- Separar sempre **evento observado** de **causa inferida**. Origem padrão: `UNKNOWN`. Nunca inventar causalidade.
- Origens: USER, APPLICATION, SYSTEM, AUTOMATION, UNKNOWN. Confiança: ALTA/MÉDIA/BAIXA (categórica).
- Windows: APIs nativas e eventos; sem polling contínuo sem justificativa.
- Entrada de teclado/mouse: registrar apenas metadados, nunca o conteúdo digitado.
- Antes de alterar: inspecionar código existente, reutilizar, menor mudança possível, sem dependência nova sem necessidade.
- Uma etapa por vez; validar (teste/execução) antes de concluir; não afirmar que funciona sem evidência.
- Não implementar o que não foi pedido. Não mascarar erros com tratamento genérico.
- Resposta final: o que mudou, arquivos, problemas, validação, próximo passo.
