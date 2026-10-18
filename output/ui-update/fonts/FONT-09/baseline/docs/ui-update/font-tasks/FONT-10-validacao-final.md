# FONT-10 — Validação final, build e contrato entregue

Status: **pendente**. Prioridade: P0 para encerrar o lote. Dependência: FONT-09 e aceite de FONT-01 a FONT-08.

## Objetivo

Comprovar a substituição em toda a UI de produção e registrar o que foi entregue. Assets importados e testes anteriores não comprovam a aparência desta versão.

## Implementação e verificações

- [ ] Ler o [índice](README.md), o inventário e o contrato proposto. Auditar todos os consumidores e pesquisar fontes/GUIDs antigos, incluindo fontes padrão, skins, builders e runtime. Registrar exceções legítimas; não apagar arquivos com uso fora do escopo.
- [ ] Conferir a cobertura UI-01 a UI-20, com capturas reais após renderização no Editor: identificação, localização, restaurante, preços, banco, equipamentos, equipe, revisão, hub, estabelecimento, eventos/histórico, DRE mensal/trimestral, final e falência, mais diálogos e feedback.
- [ ] Comparar com os cinco concepts na mesma escala, observando desenho/peso das letras, altura aparente, largura, hierarquia, entrelinha e espaçamento. Corrigir divergências tipográficas ainda perceptíveis no lote.
- [ ] Validar 1920×1080, 1366×768, 1280×720 e 1600×1000, mais a viewport móvel landscape/safe area já usada no projeto. Registrar dimensões concretas e se a área segura foi simulada ou física.
- [ ] Testar nomes longos, acentos/descendentes, glifos do corpus, moedas largas, sinais, quantidade, erro, selecionado/desabilitado, scroll e atualização/reentrada. Auditar logs de glifo ausente e fallback inesperado.
- [ ] Executar os testes existentes relevantes de UI/fluxo e verificações novas somente para lacunas reais, como resolução de papéis, cobertura de glifos e persistência da fonte após bind. Não eliminar asserts úteis para fazer a migração passar.
- [ ] Em dados isolados, verificar abertura, edição, retomada, três fechamentos, conclusão, falência e confirmações. Preservar os dados de trabalho do usuário.
- [ ] Gerar e executar build Windows no perfil existente para confirmar inclusão de atlas/materiais e funcionamento sem fonte instalada no sistema. Validar o build/perfil Android se o toolchain permitir; runtime em aparelho só pode ser declarado testado quando realmente executado.
- [ ] Promover os valores aferidos de `font-contract.json` para `visual-contract.json`, rever tokens/comentários ativos e documentação que ainda prescreva Inter Tight. Preservar QAs históricos com suas datas.

## Entregáveis

Galeria em `output/ui-update/fonts/index.html`, matriz tela × resolução × estado, logs/resultados de teste/build, auditoria de fontes, parâmetros finais e `docs/ui-update/qa/FONT-10.json`. Atualizar o status deste lote em seu índice, no apontamento de `tasks.json` e no `COMECE-AQUI.txt` somente quando as evidências sustentarem o aceite.

## Aceite

- [ ] Todos os consumidores ativos cobertos, sem font/material nulo, glifo ausente, corte de texto ou Inter Tight primária por esquecimento.
- [ ] Roboto/Roboto Condensed e seus pesos corretos aparecem no Player e permanecem após reentrada/atualização.
- [ ] As cinco referências possuem comparação visual real; diferenças restantes e limites de plataforma são descritos com precisão.
- [ ] Download, fontes, licenças, TMP, integração e QA estão concluídos. Nenhuma pendência necessária está escondida por um status “concluído”.

## Comando para execução

> Execute FONT-10 no projeto D:/Cogna/jogos_empresa após o aceite das demais tarefas. Audite toda a migração, corrija o que faltar, compare as telas reais aos concepts, valide build/fluxo e entregue a galeria e o contrato final.
