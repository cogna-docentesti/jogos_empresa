# Ferramentas de arte

`validate_assets.py` verifica o plano em `docs/art` e os PNGs finais em `Assets/Art/new`. As referências e fontes editáveis ficam em `art-source`. Requer Python 3.10+ e Pillow.

```powershell
python tools/art/validate_assets.py --plan
python tools/art/validate_assets.py --task ART-01
```

Fora da raiz do projeto, use `--project D:/Cogna/jogos_empresa`. O argumento antigo `--root D:/Cogna/jogos_empresa/Assets/Art/new` continua aceito.

`archive/build_initial_plan.py` preserva o gerador usado no planejamento inicial e bloqueia execução. O manifesto vivo deve ser atualizado por lote; executar novamente o bootstrap perderia os status de produção e QA.
