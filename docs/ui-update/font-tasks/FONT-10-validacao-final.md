# FONT-10 — Validação final, build e contrato entregue

Status: **entregue e homologado no escopo tipográfico em 18/10/2026, com ressalvas de plataforma**. Prioridade: P0 para encerrar o lote. Dependência: FONT-09 e aceite de FONT-01 a FONT-08.

## Objetivo

Comprovar a substituição em toda a UI de produção e registrar o que foi entregue. Assets importados e testes anteriores não comprovam a aparência desta versão.

## Implementação e verificações

- [x] Ler o [índice](README.md), o inventário e o contrato proposto. Auditar todos os consumidores e pesquisar fontes/GUIDs antigos, incluindo fontes padrão, skins, builders e runtime. Registrar exceções legítimas; não apagar arquivos com uso fora do escopo.
- [x] Conferir a cobertura UI-01 a UI-20, com capturas reais após renderização no Editor: identificação, localização, restaurante, preços, banco, equipamentos, equipe, revisão, hub, estabelecimento, eventos/histórico, DRE mensal/trimestral, final e falência, mais diálogos e feedback.
- [x] Comparar com os cinco concepts na mesma escala, observando desenho/peso das letras, altura aparente, largura, hierarquia, entrelinha e espaçamento. Corrigir divergências tipográficas ainda perceptíveis no lote.
- [x] Validar 1920×1080, 1366×768, 1280×720 e 1600×1000, mais a viewport móvel landscape/safe area já usada no projeto. Registrar dimensões concretas e se a área segura foi simulada ou física.
- [x] Testar nomes longos, acentos/descendentes, glifos do corpus, moedas largas, sinais, quantidade, erro, selecionado/desabilitado, scroll e atualização/reentrada. Auditar logs de glifo ausente e fallback inesperado.
- [x] Executar os testes existentes relevantes de UI/fluxo e verificações novas somente para lacunas reais, como resolução de papéis, cobertura de glifos e persistência da fonte após bind. Não eliminar asserts úteis para fazer a migração passar.
- [x] Em dados isolados, verificar abertura, edição, retomada, três fechamentos, conclusão, falência e confirmações. Preservar os dados de trabalho do usuário.
- [x] Gerar e executar build Windows no perfil existente para confirmar inclusão de atlas/materiais e funcionamento sem fonte instalada no sistema. Validar o build/perfil Android se o toolchain permitir; runtime em aparelho só pode ser declarado testado quando realmente executado.
- [x] Promover os valores aferidos de `font-contract.json` para `visual-contract.json`, rever tokens/comentários ativos e documentação que ainda prescreva Inter Tight. Preservar QAs históricos com suas datas.

## Entregáveis

Galeria em `output/ui-update/fonts/index.html`, matriz tela × resolução × estado, logs/resultados de teste/build, auditoria de fontes, parâmetros finais e `docs/ui-update/qa/FONT-10.json`. Atualizar o status deste lote em seu índice, no apontamento de `tasks.json` e no `COMECE-AQUI.txt` somente quando as evidências sustentarem o aceite.

## Aceite

- [x] Todos os consumidores ativos cobertos, sem font/material nulo, glifo ausente, corte de texto ou Inter Tight primária por esquecimento.
- [x] Roboto/Roboto Condensed e seus pesos corretos aparecem no Player e permanecem após reentrada/atualização.
- [x] As cinco referências possuem comparação visual real; diferenças restantes e limites de plataforma são descritos com precisão.
- [x] Download, fontes, licenças, TMP, integração e QA estão concluídos. Nenhuma pendência necessária está escondida por um status “concluído”.

## Comando para execução

> Execute FONT-10 no projeto D:/Cogna/jogos_empresa após o aceite das demais tarefas. Audite toda a migração, corrija o que faltar, compare as telas reais aos concepts, valide build/fluxo e entregue a galeria e o contrato final.

## Evidências de aceite

[QA final](../qa/FONT-10.json), [galeria global](../../../output/ui-update/fonts/index.html) e [matriz tela/resolução/estado](../../../output/ui-update/fonts/FONT-10/screen-resolution-state-matrix.csv). Windows Player executado; APK Android gerado. Aparelho Android não testado. A recuperação após a falha do Editor e os hashes idênticos da sessão estão documentados no QA.

A tipografia e o fluxo renderizado passaram. O Player Windows falha na limpeza nativa ao fechar (0xc0000005); encerramento não homologado. Android mantém o aviso de entrada Both e não foi executado em aparelho. A falha de encerramento continua registrada como check falho; a promoção abrange apenas a tipografia. Roboto existe no Windows usado: foram verificados sourceFontFile nulos, atlas estáticos e fontes de cada glifo, sem afirmar teste em um sistema sem Roboto instalada.

Atualização Windows em 18/10/2026: a falha de encerramento acima foi corrigida com o Unity 6000.3.21f1 (UUM-146676). Os oito casos de saída em Development/release QA e as 254 verificações da campanha passaram, com consulta de UI Automation e saída 0. O QA original preserva o resultado histórico e referencia a correção em `shutdown_follow_up`. [Investigação e build corrigido](../../windows-shutdown.txt).
