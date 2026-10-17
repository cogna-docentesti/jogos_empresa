# FONT-01 — Inventário e matriz por texto

Status: **pendente**. Prioridade: P0. Dependências: nenhuma.

## Objetivo

Identificar todos os consumidores de tipografia na UI de produção e transformar a matriz do [índice](README.md) em um mapa executável. Esta tarefa não reabre a escolha de fontes: a base é Roboto e Roboto Condensed.

## Ler e inspecionar

- `Assets/Scenes/GameScene.unity`, `Assets/Prefabs/` e `ProjectSettings/EditorBuildSettings.asset`.
- `Assets/Scripts/Game/Adapter/In/UI/Theme/GameTypography.cs`, `SharedUiSkin.cs`, `LocationSkinV2.cs` e `LocationMockupTokens.cs`.
- `Assets/Editor/UI/`, especialmente `UiFactory`, `GlobalThemeMigration`, `IdentificationPanelBuilder`, `LocationScreenMockupBuilder`, `BackButtonStyler` e `ConceptUiRevision`.
- Os cinco concepts e a análise visual vinculados no índice.

## Implementação

- [ ] Inventariar TMP de cenas/prefabs, inclusive inativos, inputs/placeholders e textos 3D que pertençam à interface. Separar cenas antigas fora do build.
- [ ] Localizar fontes/materiais definidos em scripts, GUIDs fixos, factories, clones, `OnEnable`, skins e migrations. Não limitar a busca a nomes de família.
- [ ] Registrar cada consumidor por caminho de asset/hierarquia ou ponto de criação, papel visual, família/peso atual, destino, responsável FONT-05 a FONT-09 e estado de migração.
- [ ] Usar a matriz do índice para os destinos. Registrar diferenças justificadas por função, sem adotar uma terceira família por preferência pessoal.
- [ ] Levantar caracteres do conteúdo real, nomes editáveis, moedas, pontuação, símbolos, rich text e estilos usados. Verificar a necessidade real de itálico.
- [ ] Capturar uma referência atual de cabeçalho, botão, input, card, tabela e KPI no Editor em execução; associar a regiões dos concepts na mesma escala.

## Entregáveis

Inventário em `docs/ui-update/font-coverage.json`, matriz proposta em `docs/ui-update/font-contract.json` e evidências/QA nos destinos comuns. O contrato novo deve dizer **planejado**, sem substituir o contrato vigente antes da aplicação.

## Aceite e validação

- [ ] Todas as telas UI-01 a UI-20 têm responsável no inventário, inclusive diálogos e conteúdo criado em runtime.
- [ ] Cada texto possui papel e destino verificáveis; textos de ilustração e exceções ficam explicitados.
- [ ] O inventário confirma quais fontes e assets TMP podem ser reutilizados e o que FONT-02/FONT-03 precisam fornecer.
- [ ] Não há afirmação de que capturas antigas comprovam a nova fonte.

## Comando para execução

> Execute FONT-01 no projeto D:/Cogna/jogos_empresa. Leia este arquivo e README.md do mesmo diretório, produza o inventário, a matriz por função e o QA real; atualize os status após o aceite.
