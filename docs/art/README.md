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

O inventário contém 272 assets. ART-01 e ART-02 entregaram seis masters canônicos CH01–CH06, validados em 1024×1536 com alfa real; os outros 266 assets continuam planejados. Consulte as revisões [ART-01](qa/ART-01.json) e [ART-02](qa/ART-02.json), com prompts registrados em [ART-01](prompts/ART-01.json) e [ART-02](prompts/ART-02.json).

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
