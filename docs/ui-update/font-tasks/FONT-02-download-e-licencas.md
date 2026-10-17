# FONT-02 — Download, fontes de produção e licenças

Status: **pendente**. Prioridade: P0. Dependência: FONT-01.

## Objetivo

Garantir os arquivos corretos de Roboto e Roboto Condensed dentro do projeto, com os pesos reais necessários para criar os assets TextMeshPro.

## Entradas e destino

Ler o [índice](README.md), `font-coverage.json` e `font-contract.json`. Usar as páginas e repositórios oficiais indicados no índice. Destino proposto: `Assets/TextMesh Pro/Fonts/Concept/`, com subpastas `Sources`, `Static` e `SDF`; seguir uma estrutura equivalente já existente se adequada.

## Implementação

- [ ] Conferir fontes existentes por metadados, cobertura e licença. Validar downloads de pesquisa antes de reaproveitar; `Examples & Extras` não deve ser a dependência de produção.
- [ ] Baixar da distribuição oficial tudo que faltar: Roboto Regular 400/Bold 700 e Roboto Condensed Regular 400/Medium 500/SemiBold 600/Bold 700.
- [ ] Incluir itálicos somente se FONT-01 identificar consumidores; fornecer a face real correspondente, em vez de conservar Inter Tight por esquecimento.
- [ ] Registrar URL final, revisão/versão, nome de família/estilo e SHA-256 de cada fonte. Guardar o texto da licença e os créditos da versão efetivamente adquirida.
- [ ] Se houver somente fontes variáveis, criar instâncias estáticas reproduzíveis com ferramenta adequada, registrando versão e eixos. Roboto normal usa a largura normal; Roboto Condensed deve vir da própria família condensada.
- [ ] Inspecionar os nomes e os contornos gerados. Não aceitar seis arquivos que continuem sendo Regular nem renomear uma fonte para simular um peso.
- [ ] Importar pelo Unity e versionar `.meta` estáveis. Não adicionar todas as famílias da pesquisa ou depender de instalação no Windows.

## Entregáveis

TTF/OTF de produção, licenças e `docs/ui-update/font-sources.json`. Se houver geração de instâncias estáticas, entregar também o procedimento/script reproduzível. Esta tarefa fornece fontes; a geração TMP é FONT-03.

## Aceite e validação

- [ ] Os seis pesos/famílias previstos estão disponíveis no projeto e seus metadados correspondem à matriz.
- [ ] Origem, licenças e hashes estão registrados; arquivos já adequados não foram duplicados sem necessidade.
- [ ] A importação no Editor não apresenta erro. Nenhuma referência de produção aponta para `output` ou uma fonte do sistema operacional.

## Comando para execução

> Execute FONT-02 no projeto D:/Cogna/jogos_empresa após FONT-01. Localize ou baixe as fontes oficiais necessárias, gere instâncias estáticas quando necessário, importe-as e registre origem, licenças, metadados e QA.
