"""Create technical-only ART-24 pipelines without modifying earlier batches."""
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sources={'prepare-equipment.py':'prepare-art24.py','export-equipment.cjs':'export-art24.cjs','finalize-equipment.py':'finalize-art24.py'}
for source,dest in sources.items():
    text=(ROOT/'art-source/editable/shared'/source).read_text(encoding='utf-8')
    text=text.replace("['ART-20','ART-21']","['ART-24']")
    text=text.replace('len(selected)==4','len(selected)==3').replace('g.assets.length!==4','g.assets.length!==3')
    text=text.replace('Expected four equipment requests','Expected three equipment requests')
    text=text.replace('const width=1040,height=1310','const width=1560,height=710')
    text=text.replace('const x=(i%2)*cw,y=100+Math.floor(i/2)*600+row*cellh;','const x=i*cw,y=100+row*cellh;')
    text=text.replace('4 PNGs RGBA','3 PNGs RGBA')
    text=text.replace('art-source/editable/shared/export-equipment.cjs','art-source/editable/ART-24/export-art24.cjs')
    text=text.replace('Execute numbered batch in the manifest, continuing the prior clarification; user requested ART-21 while ART-20 was active, so finish both authorized batches in order. ART-05 controls preserved.',
        'Execute only ART-24 French equipment in the manifest, continuing the prior clarification about numbered batches. ART-05 controls and previous batches preserved.')
    text=text.replace('Follow numbered equipment batch in the manifest. Both ART-20 and ART-21 were requested; finish in order. ART-05 controls preserved.',
        'Execute only ART-24 French equipment in the manifest, continuing the prior clarification about numbered batches. ART-05 controls and previous batches preserved.')
    text=text.replace('ART-20 estava em execução quando o usuário pediu ART-21. Ambos foram autorizados e produzidos em sequência, cada qual com arquivos e registros separados. Trecho repetido sobre controles corresponde ao ART-05, preservado.',
        'Executado somente ART-24 — Equipamentos franceses, conforme manifesto e esclarecimento anterior do usuário sobre os lotes numerados. Trecho repetido sobre controles corresponde ao ART-05, preservado, assim como lotes anteriores.')
    target=ROOT/'art-source/editable/ART-24'/dest;assert not target.exists(),target
    target.write_text(text,encoding='utf-8')
print('Created ART-24 preparation, export and QA scripts; previous sources preserved.')
