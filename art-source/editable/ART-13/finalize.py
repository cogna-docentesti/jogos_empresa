"""Finalize validated ART-13 art and record scope preservation. Never edit scenes."""
from pathlib import Path
import json,hashlib,argparse
R=Path(__file__).resolve().parents[3]
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def rh(v):return hashlib.sha256(json.dumps(v,ensure_ascii=False,sort_keys=True).encode()).hexdigest()
def audit():
 b=read('docs/art/qa/ART-13-scope-baseline.json');m=read('docs/art/manifest.json');old=read(b['original_layout_path']);cur=read('docs/art/layouts/map-layout.json')
 fields=['master_size_px','coordinate_origin','camera','display_transform','zones','hit_areas','central_site_bounds_px','central_site','central_hit_area','canonical_master']
 changed=[p for p,h in b['protected_files'].items() if not (R/p).is_file() or sha(p)!=h]
 checks={'other_asset_records_unchanged':rh([a for a in m['assets'] if a['task_id']!='ART-13'])==b['other_asset_records_sha256'],
  'other_task_records_unchanged':rh([t for t in m['tasks'] if t['id']!='ART-13'])==b['other_task_records_sha256'],
  'other_tasks_index_unchanged':rh([t for t in read('docs/art/tasks.json') if t['id']!='ART-13'])==b['other_tasks_index_sha256'],
  'geometry_unchanged':all(old[k]==cur[k] for k in fields),
  'other_layers_unchanged':[l for l in old['layers'] if l['production_task']!='ART-13']==[l for l in cur['layers'] if l['production_task']!='ART-13'],
  'protected_files_unchanged':not changed,'changed_protected_files':changed,'protected_file_count':len(b['protected_files'])}
 checks['passed']=all(v for k,v in checks.items() if k.endswith('_unchanged'));write('docs/art/qa/ART-13-scope-audit.json',checks);assert checks['passed'];return checks
def finalize():
 measured=read('docs/art/qa/ART-13-measurements.json');tech=read('docs/art/qa/ART-13-technical.json');plan=read('docs/art/qa/ART-13-plan-validation.json')
 assert measured['passed'] and tech['passed'] and plan['passed'] and tech['asset_count']==13
 m=read('docs/art/manifest.json');prod=read('docs/art/prompts/ART-13.json');layout=read('docs/art/layouts/map-layout.json');compact=read('docs/art/layouts/map-selection-compact.json')
 for a in m['assets']:
  if a['task_id']!='ART-13':continue
  result=next(r for r in measured['assets'] if r['id']==a['id']);record=next(p for p in prod['assets'] if p['id']==a['id'])
  assert a['status'] in ['gerado','validado'] and a['sha256']==result['sha256']==sha(m['asset_root']+'/'+a['path'])
  assert record['source_sha256']==sha(a['source_editable'])
  a['status']='validado';a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Lote geométrico, sem personagens.',
   'record':'docs/art/qa/ART-13.json','preview':'art-source/previews/ART-13/ART-13-components-display.png' if a['kind']!='overlay' else 'art-source/previews/ART-13/ART-13-map-selections.png',
   'notes':'Alfa real, contornos, cores, estados, 9-slice e montagem no mapa revisados. Geometria canônica preservada.'}
  record['status']='validado';next(p for p in compact['assets'] if p['id']==a['id'])['status']='validado'
  if a['kind']=='overlay':
   next(l for l in layout['layers'] if l['asset_id']==a['id'])['status']='validado'
   next(l for l in compact['layers'] if l['asset_id']==a['id'])['status']='validado'
 for tasks in [m['tasks'],read('docs/art/tasks.json')]:
  next(t for t in tasks if t['id']=='ART-13')['status']='validado'
  if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
 write('docs/art/layouts/map-layout.json',layout);active_hash=sha('docs/art/layouts/map-layout.json');compact['active_contract_sha256']=active_hash
 write('docs/art/layouts/map-selection-compact.json',compact);write('docs/art/manifest.json',m)
 measured.update({'active_contract_sha256':active_hash,'visual_qa_required':False,'visual_qa_completed':True})
 for label in ['light','dark']:
  extra='art-source/previews/ART-13/ART-13-overlays-native-'+label+'.png'
  assert (R/extra).is_file()
  if extra not in measured['previews']:measured['previews'].append(extra)
 write('docs/art/qa/ART-13-measurements.json',measured)
 prod.update({'status':'validado','summary':measured['summary'],'active_contract_sha256':active_hash,
  'placement_record':'docs/art/layouts/map-selection-compact.json','qa_record':'docs/art/qa/ART-13.json',
  'build_source_sha256':sha('art-source/editable/ART-13/build-art13.cjs')})
 write('docs/art/prompts/ART-13.json',prod);scope=audit()
 qa={'task_id':'ART-13','date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica',
  'technical':{'passed':True,'asset_count':13,'report':'docs/art/qa/ART-13-technical.json','plan_report':'docs/art/qa/ART-13-plan-validation.json'},
  'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'previews':measured['previews'],
   'checks':['Seis realces derivados diretamente dos polígonos do mapa; alfa baixo e contorno interno sem bloom, neon, sombra ou construção incorporada.',
    'Treze PNGs separados em RGBA sRGB, com transparência verdadeira; treze fontes SVG independentes e editáveis.',
    'Pinos e rótulos normal/selected/disabled com máscaras alfa idênticas, mesmo tamanho e pivot. Selected violeta, normal marfim e disabled neutro.',
    'Rótulos 9-slice a 260x76 preservam os quatro cantos e mantêm área central uniforme, sem emendas.',
    'Ponteira separada branca tintável para acompanhar a cor do rótulo e evitar cópias por estado.',
    'Fundos claros/escuros, dimensões nativas e nominais revisados. Montagem conjunta das seis seleções com ART-10/11/12 revisada.',
    'Ícones ART-06 e textos aparecem somente nas provas de composição. Os PNGs do ART-13 contêm fundos/contornos e realces vazios.',
    'Polígonos, câmera, geografia, hit-areas, âncoras de labels e outros layers permanecem iguais. Realces compactos obedecem ao transform comum.']},
  'state_comparisons':measured['state_comparisons'],'schema_valid':measured['schema_valid'],'summary':measured['summary'],
  'assets':measured['assets'],'measurements':'docs/art/qa/ART-13-measurements.json','active_contract_sha256':active_hash,
  'scope_audit':'docs/art/qa/ART-13-scope-audit.json','scope_preserved':scope['passed'],'pending':[],'integration_performed':False}
 write('docs/art/qa/ART-13.json',qa)
 summary=prod['summary'];names={'OVERLAY_BANK':'Área Financeira','OVERLAY_CONDOMINIUM':'Área Residencial','OVERLAY_MARKETING':'Área Corporativa','OVERLAY_STORE':'Área Comercial','OVERLAY_UNIVERSITY':'Área Educacional','OVERLAY_CENTER':'Centro / restaurante'}
 lines=['# ART-13 — Marcadores, realces e rótulos vazios do mapa','',
  'Treze PNGs separados com transparência e treze SVGs editáveis. O lote contém seis realces compactos derivados dos polígonos canônicos, três estados de pino, três estados de rótulo vazio e uma ponteira tintável independente.','',
  '| Realce | PNG | Export (px) | X,Y no master | KB (decimais) |','| --- | --- | --- | --- | --- |']
 for r in prod['assets']:
  if r['design']['family']!='overlay':continue
  w,h=r['actual_size_px'];x,y=r['position_px'];lines.append('| '+names[r['id']]+' | ['+Path(r['output_path']).name+'](../../'+r['output_path']+') | '+str(w)+'×'+str(h)+' | '+str(x)+','+str(y)+' | '+format(r['output_bytes']/1000,'.1f').replace('.',',')+' |')
 lines+=['','As posições são o **canto superior esquerdo** de cada PNG no master 1920×1080, com origem no alto à esquerda e Y crescente para baixo. A margem transparente de 2 px está incluída. Usar pivot `[0,1]`, escala `1`, bordas zero e a transformação uniforme compartilhada do mapa. Os polígonos não foram redimensionados.','',
  'Os cinco realces de localidade são mutuamente exclusivos na ordem 10; o centro na ordem 11 pode ser ativado independentemente. Eles ficam entre o terreno e as construções. Não há sexto ID de Location.','',
  '| Componente | PNG | Export / display nominal | Bordas l,b,r,t |','| --- | --- | --- | --- |']
 for r in prod['assets']:
  if r['design']['family']=='overlay':continue
  a=next(a for a in m['assets'] if a['id']==r['id']);lines.append('| '+r['id']+' | ['+Path(r['output_path']).name+'](../../'+r['output_path']+') | '+str(r['actual_size_px'][0])+'×'+str(r['actual_size_px'][1])+' / '+str(a['display_size_px'][0])+'×'+str(a['display_size_px'][1])+' | '+str(r['nine_slice_lbrt_px'])+' |')
 lines+=['','Pinos, rótulos e ponteira usam pivot `[0.5,0.5]`. Aplicar `contain` com escala uniforme aos pinos e à ponteira; aplicar `Sliced` aos rótulos. Estados normal/selecionado/desabilitado têm **máscaras alfa idênticas** por família, sem deslocamento ou mudança de bounds.','',
  'O centro dos pinos fica vazio para receber ícones ART-06 em camada independente. O slot nativo é `[80,58,96,96]`. Conteúdo dos rótulos usa texto nativo; área segura no display nominal 260×76: `[24,24,212,28]`. Os PNGs finais não têm ícones ou nomes incorporados.','',
  'A ponteira é branca tintável: aplicar a mesma cor do preenchimento do rótulo (marfim, violeta ou neutro) e desenhar sob a base, com sobreposição na junção. Reutilizar esse único sprite nos três estados.','',
  '[Contrato de seleção, componentes e encaixe](layouts/map-selection-compact.json) · [Contrato completo do mapa](layouts/map-layout.json)','',
  'No parent master, usar `anchoredPosition = [x,-y]` para os realces com âncoras no alto à esquerda e `sizeDelta` igual ao PNG. O parent mantém escala comum de `2/3` e offset absoluto de canvas `[24,176]`. A viewport 1280×848 contém o mapa 1280×720 com margem vertical local de 64 px.','',
  'A prova de montagem dos marcadores usa pinos 80×80 e rótulos 260×76 **em pixels do master**; na viewport eles ocupam 53,33×53,33 e 173,33×50,67. A revisão isolada também mostra os tamanhos nominais de UI 80×80 e 260×76. A proposta de instâncias é documentada no JSON, preservando as âncoras canônicas. Esses valores servem à futura integração.','',
  'Os treze PNGs somam **'+format(summary['png_bytes']/1000,'.1f').replace('.',',')+' KB**. Os seis realces usam '+str(summary['overlay_texels'])+' pixels, **'+format(summary['overlay_texel_reduction_percent'],'.1f').replace('.',',')+'% menos** que seis canvases 1920×1080. A capacidade total RGBA8 sem compressão/mipmaps é cerca de **'+format(summary['rgba8_capacity_bytes']/1048576,'.2f').replace('.',',')+' MiB**. Build e memória real dependem de import, plataforma e atlas.','',
  '[Componentes sobre fundos claro/escuro](../../art-source/previews/ART-13/ART-13-components-display.png) · [Geometria nativa dos marcadores](../../art-source/previews/ART-13/ART-13-markers-native.png) · [Realces sobre fundo claro](../../art-source/previews/ART-13/ART-13-overlays-native-light.png) · [Realces sobre fundo escuro](../../art-source/previews/ART-13/ART-13-overlays-native-dark.png)','',
  '[Prova das seis seleções](../../art-source/previews/ART-13/ART-13-map-selections.png) · [Exibição da área financeira](../../art-source/previews/ART-13/OVERLAY_BANK-map-display-light.png) · [Realce central sobre fundo escuro](../../art-source/previews/ART-13/OVERLAY_CENTER-map-display-dark.png)','',
  'Produção vetorial determinística, conforme a skill para componentes geométricos. Fontes em `art-source/editable/location/markers` e `art-source/editable/location/overlays`; PNGs rasterizados diretamente na resolução registrada com sharp/libvips e perfil sRGB. Não foi necessário gerar novas ilustrações; modelo registrado como `null`.','',
  '[Prompts/specs exatos, fontes e SHA256](prompts/ART-13.json) · [Gerador dos SVGs e PNGs](../../art-source/editable/ART-13/build-art13.cjs) · [QA](qa/ART-13.json) · [Medições](qa/ART-13-measurements.json) · [Auditoria](qa/ART-13-scope-audit.json)','',
  'Validação técnica e do plano aprovadas; fontes SVG e hashes conferidos. Geometria alfa dos estados idêntica, cantos do 9-slice preservados, sem emendas no centro. SVGs dos realces usam os pontos exatos do contrato e não têm efeitos, textos ou imagens incorporados. A cobertura antialias fica limitada à borda geométrica subpixel do polígono.','',
  'Pendências de produção: nenhuma. Integração às cenas fica para etapa própria; geografia, IDs, economia, scripts e outros lotes foram preservados.']
 lines+=['','Fontes SVG separadas:','']
 for r in prod['assets']:lines.append('- ['+r['id']+'](../../'+r['source_editable']+')')
 (R/'docs/art/ART-13-entregas.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print(json.dumps({'task_id':'ART-13','status':'validado','png_count':13,'svg_count':13,'png_bytes':summary['png_bytes'],'pending':[],'scope_preserved':scope['passed']}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--visual-reviewed',action='store_true');args=p.parse_args()
 if not args.visual_reviewed:p.error('Inspecionar previews e montagens antes da finalização.')
 finalize()
