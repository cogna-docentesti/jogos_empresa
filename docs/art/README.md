# Produção de arte Cogna

Abra [PLANO-DE-ARTE.html](PLANO-DE-ARTE.html) para consultar o inventário e os comandos dos 40 lotes. Solicite um lote por vez ao agente `$cogna-artista`.

| Pasta na raiz do projeto | Conteúdo |
| --- | --- |
| `Assets/Art/new/` | PNGs finais que o Unity importa, separados por interface e função |
| `docs/art/` | Plano, manifesto, tarefas, especificações, schemas, contratos de montagem, prompts e registros de QA |
| `art-source/references/` | Concepts e referências aprovadas |
| `art-source/editable/` | Fontes SVG/código e arquivos de trabalho editáveis |
| `art-source/previews/` | Provas de montagem e revisão visual |
| `tools/art/` | Validador e ferramentas de produção |

O inventário contém 272 assets. Os 265 assets do núcleo (ART-01 a ART-35) estão produzidos e validados. A ART-36 concluiu a auditoria final, com revalidação dos arquivos, hashes, identidades, contratos e 87 provas separadas de 1920×1080 inspecionadas diretamente. Nenhum sprite foi gerado ou alterado nesta auditoria. As sete peças de ART-37/38/39 continuam com seus estados próprios; ART-40 depende de briefing e não define sprites.

Consulte a [entrega ART-36](ART-36-entregas.md), a [galeria de provas](../../art-source/previews/ART-36/index.html), o [QA](qa/ART-36.json) e o [mapa de integração por asset](qa/ART-36-integration-map.json). Prompts e revisões dos lotes de produção permanecem em `prompts/ART-XX.json` e `qa/ART-XX.json`.

**Integração no jogo pendente.** A ART-36 encontrou propostas de importação ainda não aplicadas em 227 assets e nenhuma referência aos 265 GUIDs novos nos 175 arquivos serializados varridos. Os [detalhes de importação](qa/ART-36-import-audit.json) e as condições de CanvasScaler, tint, 9-slice e montagem constam da entrega. Nada foi aplicado às cenas ou catálogos; as provas não substituem testes no Unity.

## Caminhos do manifesto v2

As raízes declaradas no manifesto são relativas à raiz do repositório. `assets[].path` e `characters[].canonical_asset` são relativos a `asset_root` (`Assets/Art/new`). Referências, `source_editable` e caminhos dos contratos/schemas são relativos à raiz do repositório. Não salvar SVGs, prompts, previews ou documentos em `Assets/Art/new`.

O [manifest.json](manifest.json) registra o status real de cada entrega. Os arquivos em `tasks/` detalham cada lote; `tokens.json`, `characters.json`, `sources.json` e `catalogos.json` completam a especificação.

## Validação

Na raiz do projeto, com Python e Pillow disponíveis:

```powershell
python tools/art/validate_assets.py --plan
python tools/art/validate_assets.py --task ART-01
```

O primeiro comando verifica o planejamento. O segundo exige arquivos reais do lote: itens planejados ainda ausentes falham, como esperado. A revisão visual continua obrigatória.
