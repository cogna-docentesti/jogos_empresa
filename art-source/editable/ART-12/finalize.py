"""Finalize only after inspecting native/background and assembled previews."""
from pathlib import Path
import json,hashlib,argparse
R=Path(__file__).resolve().parents[3]
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def finalize():
 measured=read('docs/art/qa/ART-12-measurements.json');tech=read('docs/art/qa/ART-12-technical.json');plan=read('docs/art/qa/ART-12-plan-validation.json')
 assert measured['passed'] and tech['passed'] and plan['passed'] and tech['asset_count']==4
 m=read('docs/art/manifest.json');prod=read('docs/art/prompts/ART-12.json');layout=read('docs/art/layouts/map-layout.json');compact=read('docs/art/layouts/map-restaurants-compact.json')
 for a in m['assets']:
  if a['task_id']!='ART-12':continue
  r=next(r for r in measured['assets'] if r['id']==a['id']);p=next(p for p in prod['assets'] if p['id']==a['id'])
  assert a['status']=='gerado' and a['sha256']==r['sha256']==sha(m['asset_root']+'/'+a['path'])
  a['status']='validado';a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Não há personagens neste lote.',
   'record':'docs/art/qa/ART-12.json','preview':r['detail_preview'],'notes':'Câmera, alfa, estilo, posição de porta, implantação e montagem com ART-10/ART-11 revisados. PNG compacto com margens transparentes de 2 px.'}
  p['status']='validado';p['reference_sha256']={ref:sha(ref) for ref in p['references']};p['catalog_id']=a['catalog_id'];p['catalog_source']=a['source']
  next(l for l in layout['layers'] if l['asset_id']==a['id'])['status']='validado'
  next(e for e in compact['assets'] if e['id']==a['id'])['status']='validado'
 for tasks in [m['tasks'],read('docs/art/tasks.json')]:
  next(t for t in tasks if t['id']=='ART-12')['status']='validado'
  if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
 write('docs/art/layouts/map-layout.json',layout);active_hash=sha('docs/art/layouts/map-layout.json')
 compact['active_contract_sha256']=active_hash;write('docs/art/layouts/map-restaurants-compact.json',compact)
 measured['active_contract_sha256']=active_hash;measured['visual_qa_required']=False;measured['visual_qa_completed']=True;write('docs/art/qa/ART-12-measurements.json',measured)
 prod.update({'status':'validado','summary':measured['summary'],'active_contract':'docs/art/layouts/map-layout.json','active_contract_sha256':active_hash,
  'qa_record':'docs/art/qa/ART-12.json','placement_record':'docs/art/layouts/map-restaurants-compact.json','integration_performed':False})
 write('docs/art/manifest.json',m);write('docs/art/prompts/ART-12.json',prod)
 qa={'task_id':'ART-12','date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica',
  'technical':{'passed':True,'asset_count':4,'report':'docs/art/qa/ART-12-technical.json','plan_report':'docs/art/qa/ART-12-plan-validation.json'},
  'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'previews':measured['previews'],
   'checks':['Três variantes do mesmo restaurante baixo com cobertura plana, toldo e faixa roxos, porta comum e alterações de decoração/interior.',
    'Câmera oblíqua editorial, luz do alto/esquerda, contornos ameixa e paleta coerentes com concepts e ART-10/ART-11.',
    'Sem textos, nomes, logos, pessoas, cards ou UI rasterizados. Fachadas completas, sem cortes perceptíveis ou checkerboard.',
    'Recortes nativos em RGBA sRGB sobre fundos marfim/ameixa; montagens de cada variante na viewport 1280x848 com transformação uniforme.',
    'Implantações no lote central e envelope visível respeitados; porta livre; foreground oclui discretamente a base sem invadir as vias.',
    'Fonte com alfa original preservado; máscara alfa e RGBA de todo pixel visível reconstituídos sem diferença após recorte.',
    'Três variantes registradas na mesma matriz e porta; interseção de silhuetas maior que 99%. Não se declara identidade pixel a pixel entre imagens geradas.']},
  'schema_valid':measured['schema_valid'],'measurements':'docs/art/qa/ART-12-measurements.json','variant_comparisons':measured['variant_comparisons'],
  'summary':measured['summary'],'assets':measured['assets'],'active_contract_sha256':active_hash,'pending':[],
  'integration_performed':False,'scope_audit':'docs/art/qa/ART-12-scope-audit.json'}
 write('docs/art/qa/ART-12.json',qa)
 lines=['# ART-12 — Restaurantes centrais e camada frontal','',
  'Quatro PNGs compactos com transparência, produzidos seguindo os concepts aprovados. Os três restaurantes usam a mesma construção, câmera, escala e entrada; a decoração e o interior diferenciam lanches, francês e japonês. A vegetação com banco é uma camada separada de primeiro plano.','',
  '| Asset | PNG em Assets/Art/new | Export (px) | X,Y no master | KB (decimais) |','| --- | --- | --- | --- | --- |']
 names={'MAP_REST_LANCHES':'Lanches','MAP_REST_FRANCES':'Francês','MAP_REST_JAPONES':'Japonês','MAP_FOREGROUND':'Vegetação e banco'}
 for p in prod['assets']:
  w,h=p['actual_size_px'];x,y=p['position_px'];lines.append('| '+names[p['id']]+' | ['+Path(p['output_path']).name+'](../../'+p['output_path']+') | '+str(w)+'×'+str(h)+' | '+str(x)+','+str(y)+' | '+format(p['output_bytes']/1000,'.1f').replace('.',',')+' |')
 summary=measured['summary']
 lines += ['',
  'As posições são o **canto superior esquerdo do PNG**, em pixels do master 1920×1080, com origem no alto à esquerda e Y crescente para baixo. Todos usam pivot `[0,1]`, escala `1`, bordas zero e margem transparente de 2 px. A posição inclui a margem.','',
  'Mostrar somente um restaurante por vez na ordem de camada 70. A vegetação usa ordem 90. Porta comum `[1280,642]`; referência comum de chão `[1172,657]`. A âncora de chão é uma referência de montagem do master, não o canto do sprite. Como os recortes têm dimensões distintas, **cada variante usa sua própria posição top-left**, registrada na tabela e no JSON.','',
  '[Contrato de encaixe dos quatro recortes](layouts/map-restaurants-compact.json) · [Contrato completo do mapa](layouts/map-layout.json)','',
  'No parent master, usar RectTransform com âncoras no alto à esquerda, pivot `[0,1]`, `anchoredPosition = [x,-y]` e `sizeDelta` igual ao PNG. O JSON também registra as posições para pivot central e para a viewport. Aplicar ao parent a transformação uniforme comum de `2/3` com offset absoluto de canvas `[24,176]`. A viewport 1280×848 contém o mapa 1280×720 com 64 px de margem vertical local.','',
  'Os quatro PNGs somam **'+format(summary['png_bytes']/1000,'.1f').replace('.',',')+' KB**. Os restaurantes têm cerca de 112–115 KB cada; o foreground tem cerca de 16 KB. A área total das texturas é **'+str(summary['total_texels'])+' pixels**, '+format(summary['texel_reduction_percent'],'.1f').replace('.',',')+'% menor que quatro canvases 1920×1080. A capacidade RGBA8 sem compressão/mipmaps é cerca de **0,99 MiB**, ante 31,64 MiB dos quatro canvases. O tamanho do aplicativo e a memória real dependem do import, compressão, plataforma e atlas.','',
  'As fontes maiores ficam em `art-source/references/generations/ART-12`, fora dos assets importáveis. Somente os quatro recortes compactos deste lote foram adicionados em `Assets/Art/new`. Import sugerido para integração: Sprite Single, sRGB, alfa como transparência, sem mipmaps, PPU 100, filtro bilinear e bordas zero; max texture size >= dimensão real (512 para os restaurantes, 256 para o foreground). Nenhum import ou cena foi aplicado nesta entrega.','',
  '[Prévia sobre fundo claro](../../art-source/previews/ART-12/ART-12-contact-light.png) · [Prévia sobre fundo escuro](../../art-source/previews/ART-12/ART-12-contact-dark.png) · [Montagem de lanches](../../art-source/previews/ART-12/MAP_REST_LANCHES-display-light.png) · [Montagem francesa](../../art-source/previews/ART-12/MAP_REST_FRANCES-display-dark.png) · [Montagem japonesa](../../art-source/previews/ART-12/MAP_REST_JAPONES-display-light.png)','',
  'Ilustrações geradas por ImageGen integrado, um asset por chamada; francês e japonês são edições da construção de lanches. O modelo não foi exposto pela ferramenta e está registrado como `null`. O export técnico usa sharp com uma única redução uniforme, preservação do alfa original e recorte lossless. Não houve pintura, deformação, máscaras ou limpeza de alfa por código.','',
  '[Prompts exatos, fontes, medidas e SHA256](prompts/ART-12.json) · [Exportador técnico](../../art-source/editable/ART-12/export-compact.cjs) · [QA](qa/ART-12.json) · [Medições](qa/ART-12-measurements.json) · [Auditoria de escopo](qa/ART-12-scope-audit.json)','',
  'Validação técnica dos quatro PNGs e do plano aprovada. Reconstrução do alfa e dos canais de todos os pixels com alfa não zero sem diferença. Implantação central, envelope visível, porta livre, alinhamento das variantes e montagem conjunta conferidos. Aproximadamente 1054–1136 pixels visíveis da base são ocluídos pelo foreground, conforme a variante.','',
  'Os recortes incluem resíduos de alfa muito baixo herdados da geração, sem limpeza destrutiva; por isso o retângulo total pode variar e ultrapassar levemente o envelope que limita a silhueta visível. Esses pixels também foram preservados e contabilizados. O arquivo japonês contém uma margem inferior pouco visível maior que a francesa, sem alterar a implantação.','',
  'Pendências de produção: nenhuma. A aplicação às cenas fica para a etapa de integração; geografia, câmera, hit-areas, IDs e outros lotes foram preservados.']
 (R/'docs/art/ART-12-entregas.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print(json.dumps({'task_id':'ART-12','status':'validado','png_count':4,'bytes':summary['png_bytes'],'pending':[]}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--visual-reviewed',action='store_true');args=p.parse_args()
 if not args.visual_reviewed:p.error('Inspecionar previews e montagens antes da finalização.')
 finalize()
