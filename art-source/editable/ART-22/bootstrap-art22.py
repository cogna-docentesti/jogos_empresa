"""Copy proven technical-only pipelines for ART-22 without changing prior batches."""
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sources={
 'prepare-equipment.py':'prepare-art22.py',
 'export-equipment.cjs':'export-art22.cjs',
 'finalize-equipment.py':'finalize-art22.py'
}
for source,dest in sources.items():
    text=(ROOT/'art-source/editable/shared'/source).read_text(encoding='utf-8')
    text=text.replace("['ART-20','ART-21']","['ART-22']")
    text=text.replace('len(selected)==4','len(selected)==2')
    text=text.replace('g.assets.length!==4','g.assets.length!==2')
    text=text.replace('Expected four equipment requests','Expected two equipment requests')
    text=text.replace('const width=1040,height=1310','const width=1040,height=710')
    text=text.replace('4 PNGs RGBA','2 PNGs RGBA')
    text=text.replace('art-source/editable/shared/export-equipment.cjs','art-source/editable/ART-22/export-art22.cjs')
    text=text.replace('Execute numbered batch in the manifest, continuing the prior clarification; user requested ART-21 while ART-20 was active, so finish both authorized batches in order. ART-05 controls preserved.',
        'Execute only ART-22 snack-kitchen equipment from the manifest, continuing the prior clarification about numbered batches. ART-05 controls and prior equipment batches preserved.')
    text=text.replace('Follow numbered equipment batch in the manifest. Both ART-20 and ART-21 were requested; finish in order. ART-05 controls preserved.',
        'Execute only ART-22 equipment in the manifest, continuing the prior clarification about numbered batches. ART-05 controls and prior equipment batches preserved.')
    text=text.replace('ART-20 estava em execução quando o usuário pediu ART-21. Ambos foram autorizados e produzidos em sequência, cada qual com arquivos e registros separados. Trecho repetido sobre controles corresponde ao ART-05, preservado.',
        'Executado somente ART-22 — Equipamentos de lanches, conforme manifesto e esclarecimento anterior do usuário sobre os lotes numerados. Trecho repetido sobre controles corresponde ao ART-05, preservado, assim como lotes anteriores.')
    target=ROOT/'art-source/editable/ART-22'/dest
    assert not target.exists(),target
    target.write_text(text,encoding='utf-8')
print('Created ART-22 preparation, export and QA scripts; previous source files preserved.')
