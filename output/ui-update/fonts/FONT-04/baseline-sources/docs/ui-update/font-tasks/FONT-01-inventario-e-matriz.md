# FONT-01 — Inventário e matriz por texto

Status: **concluído em 17/10/2026**. Prioridade: P0. Dependências: nenhuma.

## Objetivo

Identificar todos os consumidores de tipografia na UI de produção e transformar a matriz do [índice](README.md) em um mapa executável. Esta tarefa não reabre a escolha de fontes: a base é Roboto e Roboto Condensed.

## Ler e inspecionar

- `Assets/Scenes/GameScene.unity`, `Assets/Prefabs/` e `ProjectSettings/EditorBuildSettings.asset`.
- `Assets/Scripts/Game/Adapter/In/UI/Theme/GameTypography.cs`, `SharedUiSkin.cs`, `LocationSkinV2.cs` e `LocationMockupTokens.cs`.
- `Assets/Editor/UI/`, especialmente `UiFactory`, `GlobalThemeMigration`, `IdentificationPanelBuilder`, `LocationScreenMockupBuilder`, `BackButtonStyler` e `ConceptUiRevision`.
- Os cinco concepts e a análise visual vinculados no índice.

## Implementação

- [x] Inventariar TMP de cenas/prefabs, inclusive inativos, inputs/placeholders e textos 3D que pertençam à interface. Separar cenas antigas fora do build.
- [x] Localizar fontes/materiais definidos em scripts, GUIDs fixos, factories, clones, `OnEnable`, skins e migrations. Não limitar a busca a nomes de família.
- [x] Registrar cada consumidor por caminho de asset/hierarquia ou ponto de criação, papel visual, família/peso atual, destino, responsável FONT-05 a FONT-09 e estado de migração.
- [x] Usar a matriz do índice para os destinos. Registrar diferenças justificadas por função, sem adotar uma terceira família por preferência pessoal.
- [x] Levantar caracteres do conteúdo real, nomes editáveis, moedas, pontuação, símbolos, rich text e estilos usados. Verificar a necessidade real de itálico.
- [x] Capturar uma referência atual de cabeçalho, botão, input, card, tabela e KPI no Editor em execução; associar a regiões dos concepts na mesma escala.

## Entregáveis

Inventário em `docs/ui-update/font-coverage.json`, matriz proposta em `docs/ui-update/font-contract.json` e evidências/QA nos destinos comuns. O contrato novo deve dizer **planejado**, sem substituir o contrato vigente antes da aplicação.

## Aceite e validação

- [x] Todas as telas UI-01 a UI-20 têm responsável no inventário, inclusive diálogos e conteúdo criado em runtime.
- [x] Cada texto possui papel e destino verificáveis; textos de ilustração e exceções ficam explicitados.
- [x] O inventário confirma quais fontes e assets TMP podem ser reutilizados e o que FONT-02/FONT-03 precisam fornecer.
- [x] Não há afirmação de que capturas antigas comprovam a nova fonte.

## Resultado verificado

- [Inventário por consumidor](../font-coverage.json): 1.233 TMP serializados (1.051 em GameScene), 1.323 posições de runtime, 1.094 pontos de fonte/conteúdo/ciclo de vida em código e 214 campos de conteúdo de catálogos. Global IDs distinguem os serializados; índices de irmãos distinguem hierarquias e clones com nomes repetidos. Não há TMP 3D no escopo.
- [Contrato planejado](../font-contract.json): seis faces de Roboto/Roboto Condensed, destinos por papel e tamanho atual de cada texto como partida. Nenhum itálico necessário no conteúdo encontrado. FONT-02 precisa verificar/reobter os arquivos e licenças de produção; nenhum TMP existente foi aprovado como primário novo.
- [QA real](../qa/FONT-01.json): Unity 6000.3.20f1 em Play Mode, 1920×1080, campanhas e diálogos com 261 checks aprovados, 86 capturas novas e zero erros de console. [Galeria](../../../output/ui-update/fonts/FONT-01/index.html) com seis categorias obrigatórias e complementos de mapa/trimestral, coordenadas, hashes e escala uniforme dos cinco concepts.
- Os 773 arquivos preexistentes conferidos mantiveram seus bytes. A cena não salva do Editor, incluindo um TMP branco “Restaurante” adicional, foi preservada em memória e não foi gravada por esta tarefa. Os patches iniciais completos permanecem locais fora do commit.
- As capturas comprovam o baseline atual em Inter Tight; a aplicação e homologação de Roboto continuam nas tarefas seguintes. Não houve build nem QA de outras viewports nesta tarefa.

## Comando para execução

> Execute FONT-01 no projeto D:/Cogna/jogos_empresa. Leia este arquivo e README.md do mesmo diretório, produza o inventário, a matriz por função e o QA real; atualize os status após o aceite.
