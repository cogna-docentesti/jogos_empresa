# FONT-02 — Download, fontes de produção e licenças

Status: **concluído em 17/10/2026** — [QA FONT-02](../qa/FONT-02.json). Prioridade: P0. Dependência: FONT-01.

## Objetivo

Garantir os arquivos corretos de Roboto e Roboto Condensed dentro do projeto, com os pesos reais necessários para criar os assets TextMeshPro.

## Entradas e destino

Ler o [índice](README.md), `font-coverage.json` e `font-contract.json`. Usar as páginas e repositórios oficiais indicados no índice. Destino proposto: `Assets/TextMesh Pro/Fonts/Concept/`, com subpastas `Sources`, `Static` e `SDF`; seguir uma estrutura equivalente já existente se adequada.

## Implementação

- [x] Conferir fontes existentes por metadados, cobertura e licença. Validar downloads de pesquisa antes de reaproveitar; `Examples & Extras` não deve ser a dependência de produção.
- [x] Baixar da distribuição oficial tudo que faltar: Roboto Regular 400/Bold 700 e Roboto Condensed Regular 400/Medium 500/SemiBold 600/Bold 700.
- [x] Incluir itálicos somente se FONT-01 identificar consumidores; fornecer a face real correspondente, em vez de conservar Inter Tight por esquecimento. FONT-01 identificou zero consumidores; nenhuma face itálica incluída.
- [x] Registrar URL final, revisão/versão, nome de família/estilo e SHA-256 de cada fonte. Guardar o texto da licença e os créditos da versão efetivamente adquirida.
- [x] Se houver somente fontes variáveis, criar instâncias estáticas reproduzíveis com ferramenta adequada, registrando versão e eixos. Roboto normal usa a largura normal; Roboto Condensed deve vir da própria família condensada.
- [x] Inspecionar os nomes e os contornos gerados. Não aceitar seis arquivos que continuem sendo Regular nem renomear uma fonte para simular um peso.
- [x] Importar pelo Unity e versionar `.meta` estáveis. Não adicionar todas as famílias da pesquisa ou depender de instalação no Windows.

## Entregáveis

TTF/OTF de produção, licenças e `docs/ui-update/font-sources.json`. Se houver geração de instâncias estáticas, entregar também o procedimento/script reproduzível. Esta tarefa fornece fontes; a geração TMP é FONT-03.

## Aceite e validação

- [x] Os seis pesos/famílias previstos estão disponíveis no projeto e seus metadados correspondem à matriz.
- [x] Origem, licenças e hashes estão registrados; arquivos já adequados não foram duplicados sem necessidade.
- [x] A importação no Editor não apresenta erro. Nenhuma referência de produção aponta para `output` ou uma fonte do sistema operacional.

## Resultado verificado

- [Manifesto](../font-sources.json): URLs finais oficiais fixadas por commit, SHA-256, versões, eixos, nomes, cobertura e auditoria de reaproveitamento.
- Produção em `Assets/TextMesh Pro/Fonts/Concept/Static`: Roboto Regular/Bold, Roboto Condensed Regular/Medium/SemiBold/Bold. Originais variáveis, licenças integrais OFL 1.1 e metadados oficiais em `Sources`; créditos em `CREDITS.txt`.
- Roboto 3.015 (2026), distribuição Google Fonts `1c627bfa375fc51cf86fabeca4f6e08a95f0aa5c`; Roboto Condensed 3.008 (2023), `69d0549e8fc7ad72e1531734c81237be401ec4fd`. Revisões upstream constam nos `METADATA.pb.txt` originais.
- Os dois downloads da pesquisa coincidem byte a byte com os oficiais. Cópias oficiais em `Assets` eliminam a dependência em `output`; o Bold 2.137 de Examples & Extras permanece somente como sample antigo.
- FontTools 4.60.1 aplica `wght=400/700, wdth=100` ao Roboto e `wght=400/500/600/700` à própria família Roboto Condensed. Nenhum eixo variável permanece. Registros de nomes/estilo explícitos para Unity/FreeType; todos os contornos decompostos e métricas horizontais conferidos contra instâncias oficiais serializadas. Seis hashes de contorno distintos, reprodução byte a byte e aumento estrito de área de tinta por peso na amostra.
- Unity 6000.3.20f1: seis faces reconhecidas, reimportação com GUIDs estáveis, zero erros/avisos; 21 `.meta` do lote versionados. Nenhum asset TMP criado e nenhuma cena salva.
- Sessão preservada: cena suja, 1052 TMP e texto extra `Restaurante` em memória, com fingerprint idêntico antes/depois. Baseline próprio de 9987 arquivos preexistentes conferido; alterações desta tarefa em arquivos existentes limitadas a este documento e ao índice.
- [Evidências](../../../output/ui-update/fonts/FONT-02/index.html): verificação binária, relatório do Editor, metas e amostra visual de TTF. A amostra não homologa atlas TMP nem telas de produção.

### Cobertura para FONT-03

Todas as faces cobrem 117/118 caracteres atuais e 342/536 do corpus primário ampliado da FONT-01. Falta `U+2190` (←) no conteúdo atual; o ampliado também inclui outras setas e caracteres Latin Extended B ausentes. As listas completas por face estão em `missing_actual` e `missing_primary` no manifesto. FONT-03 deve garantir glifos/fallback explícitos, sem recorrer silenciosamente à Inter Tight ou ao Windows. Esta entrega não afirma cobertura integral de nomes livres nem integração da tipografia na UI.

## Reprodução

Executar na raiz do projeto, com Python 3.10. As versões das ferramentas são fixadas; as fontes e licenças originais já estão versionadas.

```powershell
python -m pip install --target output/ui-update/fonts/FONT-02/deps -r tools/ui/requirements-font02.txt
$env:PYTHONPATH = (Resolve-Path 'output/ui-update/fonts/FONT-02/deps').Path
# Opcional: baixar novamente os mesmos commits oficiais; a geração confere os hashes do manifesto.
python tools/ui/font02_sources.py fetch
python tools/ui/font02_sources.py generate
python tools/ui/font02_sources.py verify
python tools/ui/font02_sources.py specimen
```

`generate` funciona offline com os originais versionados; `verify` regenera em memória, compara os bytes e confere os contornos/métricas. Timestamps das fontes são preservados, sem incorporar o relógio da máquina. As regras restritas ao lote em `.gitattributes` preservam os bytes oficiais e os hashes de `.meta` entre checkouts Windows, mesmo com `core.autocrlf` habilitado. Espaços finais das licenças oficiais e dos `.meta` gerados pelo Unity são mantidos.

No Editor já aberto, atualizar assets e executar `Tools > Jogos de Empresa > FONT-02 > Import and validate source faces` (`Ctrl+Alt+F2`), em Edit mode. O relatório fica em `output/ui-update/fonts/FONT-02/unity-import.json`. O verificador não salva cenas. Não abrir outro Editor contra o projeto aberto. O script `tools/ui/finalize_font02_evidence.py` fecha o QA desta execução a partir dos relatórios aprovados e do baseline local original; o baseline bruto e os pacotes de ferramentas não integram o commit.

## Comando para execução

> Execute FONT-02 no projeto D:/Cogna/jogos_empresa após FONT-01. Localize ou baixe as fontes oficiais necessárias, gere instâncias estáticas quando necessário, importe-as e registre origem, licenças, metadados e QA.
