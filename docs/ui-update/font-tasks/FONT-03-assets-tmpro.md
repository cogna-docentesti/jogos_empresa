# FONT-03 — Assets TextMeshPro, materiais e cobertura de glifos

Status: **pendente**. Prioridade: P0. Dependência: FONT-02.

## Objetivo

Criar ou reaproveitar assets TMP utilizáveis no Player para todos os pesos da matriz. Ter um TTF importado, por si só, não atende ao objetivo.

## Implementação

- [ ] Ler o [índice](README.md), o corpus de FONT-01, as fontes de FONT-02 e o código TMP da versão instalada antes de escolher APIs.
- [ ] Gerar um `TMP_FontAsset` por face/peso necessário em `Assets/TextMesh Pro/Fonts/Concept/SDF/`, com atlas e material compartilhado correspondentes. Reaproveitar assets equivalentes somente após conferir todos os requisitos.
- [ ] Preferir atlas primários estáticos com os caracteres realmente usados, incluindo português. Partir de sampling 80/padding 8, escala da face 1 e atlas 1024; ajustar tamanho/packing conforme ocupação e qualidade medidas. Usar SDF de qualidade compatível com a versão instalada.
- [ ] Configurar resolução de pesos/faces e rich text sem duplicar negrito sintético sobre uma face real. Conferir o resultado de `fontWeight`, `FontStyles.Bold` e `<b>` usados no projeto.
- [ ] Material neutro, compartilhado por asset; nenhum outline, underlay ou dilatação adicional herdada para simular peso. Preservar a cor controlada pelo texto.
- [ ] Definir fallbacks apenas para caracteres fora do conjunto principal. Se dinâmicos, limitar atlas a 512–1024, configurar limpeza dos dados dinâmicos no build quando disponível e evitar ciclos ou expansão ilimitada.
- [ ] Garantir o funcionamento no Player sem fontes do sistema. Não criar um novo atlas/material a cada label ou atualização de valor.
- [ ] Salvar fontes, subassets, referências e `.meta` pelo Unity. Fornecer um caminho de regeneração idempotente, sem apagar/recriar GUIDs a cada execução.

## Validação e aceite

- [ ] Conferir visualmente todas as faces e os pesos. Regular, Medium, SemiBold e Bold devem ter diferenças reais, sem empastamento nos tamanhos menores.
- [ ] Testar o corpus de FONT-01 e, no mínimo: `ÁÉÍÓÚ ÂÊÔ ÃÕ Ç áéíóú âêô ãõ ç`, `R$ -12.345,67`, `72 / 100`, `+`, `−`, `%` e as setas/símbolos usados pela UI.
- [ ] Nenhum glifo principal vem silenciosamente de uma família antiga. Exceções e fallback usado ficam registrados.
- [ ] Criar/reimportar novamente mantém referências válidas e não duplica assets. Os assets renderizam em uma tela real; não exigir uma nova cena de demonstração.

## Entregáveis

Assets TMP completos; parâmetros, GUIDs e cobertura em `docs/ui-update/font-assets.json`; procedimento de geração e QA por tarefa.

## Comando para execução

> Execute FONT-03 no projeto D:/Cogna/jogos_empresa após FONT-02. Crie ou valide os TMP Font Assets das faces previstas, atlas, materiais, pesos e fallbacks; comprove cobertura e renderização antes de concluir.
