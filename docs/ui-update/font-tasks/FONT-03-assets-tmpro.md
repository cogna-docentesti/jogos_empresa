# FONT-03 — Assets TextMeshPro, materiais e cobertura de glifos

Status: **concluído**. Prioridade: P0. Dependência: FONT-02.

## Objetivo

Criar ou reaproveitar assets TMP utilizáveis no Player para todos os pesos da matriz. Ter um TTF importado, por si só, não atende ao objetivo.

## Implementação

- [x] Ler o [índice](README.md), o corpus de FONT-01, as fontes de FONT-02 e o código TMP da versão instalada antes de escolher APIs.
- [x] Gerar um `TMP_FontAsset` por face/peso necessário em `Assets/TextMesh Pro/Fonts/Concept/SDF/`, com atlas e material compartilhado correspondentes. Reaproveitar assets equivalentes somente após conferir todos os requisitos.
- [x] Preferir atlas primários estáticos com os caracteres realmente usados, incluindo português. Partir de sampling 80/padding 8, escala da face 1 e atlas 1024; ajustar tamanho/packing conforme ocupação e qualidade medidas. Usar SDF de qualidade compatível com a versão instalada.
- [x] Configurar resolução de pesos/faces e rich text sem duplicar negrito sintético sobre uma face real. Conferir o resultado de `fontWeight`, `FontStyles.Bold` e `<b>` usados no projeto.
- [x] Material neutro, compartilhado por asset; nenhum outline, underlay ou dilatação adicional herdada para simular peso. Preservar a cor controlada pelo texto.
- [x] Definir fallbacks apenas para caracteres fora do conjunto principal. Se dinâmicos, limitar atlas a 512–1024, configurar limpeza dos dados dinâmicos no build quando disponível e evitar ciclos ou expansão ilimitada.
- [x] Garantir o funcionamento no Player sem fontes do sistema. Não criar um novo atlas/material a cada label ou atualização de valor.
- [x] Salvar fontes, subassets, referências e `.meta` pelo Unity. Fornecer um caminho de regeneração idempotente, sem apagar/recriar GUIDs a cada execução.

## Validação e aceite

- [x] Conferir visualmente todas as faces e os pesos. Regular, Medium, SemiBold e Bold devem ter diferenças reais, sem empastamento nos tamanhos menores.
- [x] Testar o corpus de FONT-01 e, no mínimo: `ÁÉÍÓÚ ÂÊÔ ÃÕ Ç áéíóú âêô ãõ ç`, `R$ -12.345,67`, `72 / 100`, `+`, `−`, `%` e as setas/símbolos usados pela UI.
- [x] Nenhum glifo principal vem silenciosamente de uma família antiga. Exceções e fallback usado ficam registrados.
- [x] Criar/reimportar novamente mantém referências válidas e não duplica assets. Os assets renderizam em uma tela real; não exigir uma nova cena de demonstração.

## Entregáveis

Assets TMP completos; parâmetros, GUIDs e cobertura em `docs/ui-update/font-assets.json`; procedimento de geração e QA por tarefa.

## Comando para execução

> Execute FONT-03 no projeto D:/Cogna/jogos_empresa após FONT-02. Crie ou valide os TMP Font Assets das faces previstas, atlas, materiais, pesos e fallbacks; comprove cobertura e renderização antes de concluir.

## Execução FONT-03

Seis faces Roboto/Roboto Condensed e três fallbacks estáticos gerados pelo Editor. O atlas 1024 foi tentado em amostragem 80/padding 8; os parâmetros finais e as tentativas medidas estão em [font-assets.json](../font-assets.json). Materiais neutros são subassets compartilhados, sem espessura sintética. Cobertura completa de 118 caracteres atuais e 536 ampliados, com 190 glifos latinos Noto Sans e quatro setas Noto Sans Math explicitamente limitados.

Referências de Player em `Assets/Resources/UI/ConceptFontSet.asset`. Nenhuma tela foi migrada permanentemente. O teste trocou temporariamente as faces nos textos da identificação e adicionou uma folha de amostras ao Canvas existente, restaurando tudo sem salvar a cena. Capturas são Play mode com frames avançando no Game View real, com SQLite e identificação de QA isolados; não equivalem a build standalone.

### Regeneração

1. Instalar/reutilizar `tools/ui/requirements-font02.txt`; definir `PYTHONPATH` para os pacotes locais da FONT-02.
2. `python tools/ui/font03_support.py fetch` baixa revisões fixadas, verifica cobertura e gera as duas faces estáticas Noto. `generate` usa as fontes já baixadas; `verify` compara os bytes reproduzidos.
3. No Unity aberto, em Edit mode com GameScene e Game View 1920×1080, atualizar assets e executar `Tools > Jogos de Empresa > FONT-03 > Generate and validate assets`. Preferir o menu se o atalho Ctrl+Alt+F3 conflitar com uma sobreposição de GPU. Não abrir outro Editor nem salvar a cena suja para executar o QA.
4. O gerador usa `TMP_FontAsset.CreateFontAsset`, `TryAddCharacters`, API TextCore e `SerializedObject` para configurações com setters internos nesta versão. Não cria YAML de atlas. Atualiza fontes, materiais e texturas em lugar, salvando apenas os assets próprios.
5. Revisar as imagens e registrar `visual-review.json`. O empacotamento pontual de FONT-03 já foi concluído e retirado; consultar os relatórios finais preservados. Não substituir o baseline histórico para simular uma nova aceitação.

Regeneração completa e reimportação mantiveram GUIDs, IDs locais de subassets, métricas e pixels. Ver [QA FONT-03](../qa/FONT-03.json) e [galeria](../../../output/ui-update/fonts/FONT-03/index.html). Pesos não existentes na Roboto são mapeamentos de compatibilidade documentados; itálico e alfabetos fora do corpus não estão homologados. Integração por papel e builds seguem nas tarefas seguintes.
