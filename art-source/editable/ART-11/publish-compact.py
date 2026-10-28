"""Publish compact ART-11 variants and their exact placement contract, without modifying originals or scenes."""
from pathlib import Path
import argparse,hashlib,json

ROOT=Path(__file__).resolve().parents[3]
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
def write(p,v):
    target=ROOT/p;target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def rh(v):return hashlib.sha256(json.dumps(v,ensure_ascii=False,sort_keys=True).encode()).hexdigest()

def publish():
    baseline=read('docs/art/qa/ART-11-compact-baseline.json');m=read('docs/art/manifest.json')
    production=read('docs/art/prompts/ART-11-compact.json');compact=read('docs/art/layouts/map-landmarks-compact.json')
    layout=read('docs/art/layouts/map-layout.json')
    assert sha(ROOT/'docs/art/layouts/map-layout.json')==baseline['fullframe_contract_sha256']
    for old in baseline['fullframe_assets']:
        a=next(a for a in m['assets'] if a['id']==old['id'])
        assert a==old
        r=next(r for r in production['assets'] if r['id']==a['id'])
        assert sha(ROOT/r['output_path'])==r['sha256'] and sha(ROOT/r['source_path'])==old['sha256']
        a.update({'path':r['path'],'export_size_px':r['actual_size_px'],'actual_size_px':r['actual_size_px'],
            'display_size_px':r['viewport_display_rect']['size_px'],'status':'gerado','sha256':r['sha256'],
            'pivot':[0,1],'display_fit':'uniform_map_transform','texture_storage':'cropped',
            'map_position_px':r['position_px'],'master_crop_rect_px':r['crop_rect_px'],'map_master_size_px':[1920,1080],
            'brief':'Construção/bairro isolado com transparência. Recorte técnico sem reescala; posicionar pelo contrato de coordenadas do mapa.',
            'production_record':'docs/art/prompts/ART-11-compact.json','export_tool':'sharp PNG lossless crop',
            'export_source':'art-source/editable/ART-11/crop-compact.cjs',
            'placement_record':'docs/art/layouts/map-landmarks-compact.json',
            'fullframe_variant':{'path':old['path'],'export_size_px':old['export_size_px'],'sha256':old['sha256'],
                'status':'validado','production_record':'docs/art/prompts/ART-11.json','qa_record':'docs/art/qa/ART-11.json',
                'coordinate_contract':'docs/art/layouts/archive/map-layout-art11-fullframe.json'},
            'qa':{'technical':False,'visual':False,'identity':False,'record':'docs/art/qa/ART-11-compact.json'}})
        a['references']=list(dict.fromkeys(old['references']+[r['source_path']]))
        layer=next(x for x in layout['layers'] if x['asset_id']==a['id'])
        layer.update({'position_px':r['position_px'],'texture_size_px':r['actual_size_px'],'texture_storage':'cropped',
            'crop_rect_px':r['crop_rect_px'],'asset_path':r['output_path'],'pivot':[0,1],'scale':1,'status':'gerado'})
    layout['schema_version']=2
    layout['texture_storage_revision']={'date':'2026-10-14','task_id':'ART-11','request':'Construções compactas com posições exatas; preservar arte e encaixe.',
        'previous_contract':'docs/art/layouts/archive/map-layout-art11-fullframe.json','previous_sha256':baseline['fullframe_contract_sha256'],
        'master_geometry_unchanged':True,'landmarks_contract':'docs/art/layouts/map-landmarks-compact.json'}
    layout['layer_notes']['full_frame']='O master continua 1920x1080. Camadas full-frame usam position_px [0,0]; texturas compactas usam seu canto superior esquerdo em position_px, tamanho texture_size_px, escala 1 e pivot [0,1]. Aplicar a transformação uniforme do mapa ao parent.'
    layout['layer_notes']['future_status']='Camadas futuras são somente o plano de encaixe. ART-11 usa PNGs compactos; os demais lotes preservam suas especificações atuais.'
    note='Cinco construções/bairros em PNGs compactos transparentes; posições exatas no master pelo map-layout.json e map-landmarks-compact.json. Originais full-frame preservados como variantes.'
    for tasks in [m['tasks'],read('docs/art/tasks.json')]:
        task=next(t for t in tasks if t['id']=='ART-11')
        task.update({'status':'gerado','note':note,'qa_record':'docs/art/qa/ART-11-compact.json','delivery_record':'docs/art/ART-11-entregas.md'})
        if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
    write('docs/art/manifest.json',m);write('docs/art/layouts/map-layout.json',layout)
    compact['active_contract']='docs/art/layouts/map-layout.json';compact['active_contract_sha256']=sha(ROOT/'docs/art/layouts/map-layout.json')
    write('docs/art/layouts/map-landmarks-compact.json',compact)
    lines=['ART-11 — Construções e bairros do mapa (export compacto)','Escopo: nucleo','Pré-requisito: ART-10','',note,
        'A revisão compacta substitui o canvas vazio por coordenadas de encaixe, conforme pedido do usuário. Não redesenhar as construções. Não alterar cenas ou IDs.','',
        'CONTRATOS','docs/art/layouts/map-layout.json','docs/art/layouts/map-landmarks-compact.json','Origem: top_left_y_down; retângulos [x,y,width,height]; posição é canto superior esquerdo da textura.','',
        'ENTREGÁVEIS']
    for r in production['assets']:
        lines += [r['id']+' | '+r['path'],'Export '+str(r['actual_size_px'][0])+'x'+str(r['actual_size_px'][1])+' px | alfa recorte | pivot [0,1] | bordas [0,0,0,0]',
                  'Posição no master 1920x1080: '+str(r['position_px'])+' | escala 1',
                  'Âncora de chão no master: '+str(r['ground_anchor_master_px'])+' | local no PNG: '+str(r['ground_anchor_local_px']),'']
    lines += ['EXIBIÇÃO','Parent master 1920x1080 com escala comum de 2/3 e offset do contrato. Não centralizar sprites nem aplicar preserveAspect à caixa inteira 1280x848 em cada construção.',
              'VALIDAÇÃO','python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-11',
              'python art-source/editable/qa/ART-11-compact-preview.py','Reconstrução deve coincidir pixel a pixel com os full-frames preservados.','',
              'Histórico: docs/art/tasks/ART-11-fullframe.txt e docs/art/ART-11-fullframe-entregas.md.']
    (ROOT/'docs/art/tasks/ART-11.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps({'task':'ART-11','revision':'compact','status':'gerado','originals_preserved':True,'coordinate_contract_updated':True}))

def finalize():
    measured=read('docs/art/qa/ART-11-compact-measurements.json');technical=read('docs/art/qa/ART-11-compact-technical.json')
    assert measured['passed'] and measured['all_rgba_pixels_reconstructed_equal'] and measured['assembly_equal']
    assert technical['passed'] and technical['asset_count']==5
    m=read('docs/art/manifest.json');prod=read('docs/art/prompts/ART-11-compact.json');layout=read('docs/art/layouts/map-layout.json');compact=read('docs/art/layouts/map-landmarks-compact.json')
    for a in m['assets']:
        if a['task_id']!='ART-11':continue
        r=next(x for x in measured['assets'] if x['id']==a['id']);assert a['sha256']==sha(ROOT/m['asset_root']/a['path'])==r['sha256']
        a['status']='validado';a['qa']={'technical':True,'visual':True,'identity':False,'identity_note':'Recorte técnico de construção validada, sem personagens.',
            'record':'docs/art/qa/ART-11-compact.json','preview':'art-source/previews/ART-11/compact/ART-11-compact-contact-sheet.png',
            'details':r['detail_preview'],'rgba_pixels_reconstructed_equal':True,'notes':'Recorte lossless; posição, pivot e reconstrução exata verificados.'}
        next(x for x in layout['layers'] if x['asset_id']==a['id'])['status']='validado'
        next(x for x in prod['assets'] if x['id']==a['id'])['status']='validado'
        next(x for x in compact['assets'] if x['id']==a['id'])['status']='validado'
    for tasks in [m['tasks'],read('docs/art/tasks.json')]:
        next(t for t in tasks if t['id']=='ART-11').update({'status':'validado','qa_record':'docs/art/qa/ART-11-compact.json'})
        if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
    prod.update({'status':'validado','qa_record':'docs/art/qa/ART-11-compact.json'})
    write('docs/art/layouts/map-layout.json',layout)
    compact['active_contract_sha256']=sha(ROOT/'docs/art/layouts/map-layout.json');write('docs/art/layouts/map-landmarks-compact.json',compact)
    measured['active_contract_sha256']=compact['active_contract_sha256']
    write('docs/art/qa/ART-11-compact-measurements.json',measured)
    prod['active_contract']='docs/art/layouts/map-layout.json';prod['active_contract_sha256']=compact['active_contract_sha256']
    write('docs/art/manifest.json',m);write('docs/art/prompts/ART-11-compact.json',prod)
    qa={'task_id':'ART-11','revision':'compact','date':'2026-10-14','status':'validado','reviewer':'Codex — revisão visual e técnica',
        'technical_validation':{'passed':True,'asset_count':5,'report':'docs/art/qa/ART-11-compact-technical.json'},
        'visual_validation':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'previews':measured['previews'],
            'checks':['Cinco recortes transparentes completos, com borda de 2 px; nenhum redesenho, perda de resolução ou corte de alfa não zero.',
                'Cada PNG recomposto na posição registrada é idêntico pixel a pixel ao full-frame validado.',
                'Montagem conjunta e exibição preservam todas as construções, perspectivas, luz, caminhos e implantação central.',
                'Câmera, geometria, zonas e hit-areas do master permanecem idênticas; apenas armazenamento e posição das texturas mudaram.',
                'Dimensões, tamanho em disco, capacidade RGBA8 e pivots registrados; fontes e metadados originais preservados.']},
        'all_rgba_pixels_reconstructed_equal':True,'assembly_equal':True,'schema_valid':measured['schema_valid'],
        'active_contract_sha256':compact['active_contract_sha256'],
        'summary':prod['summary'],'assets':measured['assets'],'pending':[],'integration_performed':False}
    write('docs/art/qa/ART-11-compact.json',qa)
    print(json.dumps({'task':'ART-11','revision':'compact','status':'validado','png_count':5,'pending':[]}))

def audit():
    baseline=read('docs/art/qa/ART-11-compact-baseline.json');m=read('docs/art/manifest.json')
    original=read('docs/art/layouts/archive/map-layout-art11-fullframe.json');current=read('docs/art/layouts/map-layout.json')
    files={p:(ROOT/p).is_file() and sha(ROOT/p)==h for p,h in baseline['protected_files'].items()}
    geometry_fields=['master_size_px','coordinate_origin','camera','display_transform','zones','hit_areas','central_site_bounds_px','central_site','canonical_master']
    checks={'other_asset_records_unchanged':rh([a for a in m['assets'] if a['task_id']!='ART-11'])==baseline['other_asset_records_sha256'],
        'other_task_records_unchanged':rh([t for t in m['tasks'] if t['id']!='ART-11'])==baseline['other_task_records_sha256'],
        'other_tasks_index_unchanged':rh([t for t in read('docs/art/tasks.json') if t['id']!='ART-11'])==baseline['other_tasks_index_sha256'],
        'master_geometry_unchanged':all(current[k]==original[k] for k in geometry_fields),
        'other_layers_unchanged':[x for x in current['layers'] if x['production_task']!='ART-11']==[x for x in original['layers'] if x['production_task']!='ART-11'],
        'protected_files_unchanged':all(files.values()),'protected_file_count':len(files),'changed_protected_files':[p for p,ok in files.items() if not ok]}
    passed=all(v for k,v in checks.items() if k.endswith('_unchanged'))
    write('docs/art/qa/ART-11-compact-scope-audit.json',{'passed':passed,**checks});print(json.dumps({'passed':passed,**checks}));assert passed

def deliver():
    prod=read('docs/art/prompts/ART-11-compact.json');assert prod['status']=='validado'
    names={'MAP_BANK':'Banco','MAP_CONDOMINIUM':'Bairro residencial','MAP_MARKETING':'Área corporativa','MAP_STORE':'Comércio','MAP_UNIVERSITY':'Universidade'}
    summary=prod['summary'];before=summary['rgba8_bytes_before']/1048576;after=summary['rgba8_bytes_after']/1048576
    lines=['# ART-11 — Construções compactas','',
        'Os cinco assets principais agora são recortes das mesmas construções validadas, com transparência e posição exata de encaixe. O canvas vazio foi removido; a arte conserva sua resolução e seus pixels RGBA. Os originais 1920×1080 ficam preservados como variantes de comparação.','',
        '| Construção | PNG principal | Export (px) | X,Y no master | Arquivo (KB decimais) |',
        '| --- | --- | --- | --- | --- |']
    for r in prod['assets']:
        w,h=r['actual_size_px'];x,y=r['position_px'];kb=format(r['output_bytes']/1000,'.1f').replace('.',',')
        lines.append('| '+names[r['id']]+' | ['+Path(r['output_path']).name+'](../../'+r['output_path']+') | '+str(w)+'×'+str(h)+' | '+str(x)+','+str(y)+' | '+kb+' |')
    lines += ['',
        'As posições são o **canto superior esquerdo do PNG**, em pixels do master 1920×1080, com origem no alto à esquerda e Y crescendo para baixo. Usar escala `1` e pivot `[0,1]`. O tamanho inclui uma margem transparente de 2 pixels. Bairros/conjuntos mantêm suas construções agrupadas.','',
        '[Contrato completo do mapa](layouts/map-layout.json) · [Posições, dimensões, pivots e âncoras locais dos recortes](layouts/map-landmarks-compact.json)','',
        'Para um RectTransform no parent master 1920×1080, usar âncoras no alto à esquerda, pivot `[0,1]`, `anchoredPosition = [x,-y]` e `sizeDelta` igual ao tamanho do PNG. As posições para pivot central e para a viewport 1280×848 também estão no JSON. Esses valores são propostas de integração; nenhuma cena ou import existente foi alterado.','',
        'Aplicar ao parent do mapa a transformação compartilhada de `2/3`, conforme o contrato. A caixa 1280×848 contém o mapa 1280×720, com offset vertical local de 64 px. Cada textura compacta ocupa só sua área; a posição fornecida recompõe o layout.','',
        '[Prévia dos recortes](../../art-source/previews/ART-11/compact/ART-11-compact-contact-sheet.png) · [Montagem no master](../../art-source/previews/ART-11/compact/compact-assembly-light.png) · [Exibição sobre fundo escuro](../../art-source/previews/ART-11/compact/compact-display-dark.png)','',
        'Os PNGs somam **'+format(summary['png_bytes_after']/1000,'.1f').replace('.',',')+' KB**, ante '+format(summary['png_bytes_before']/1000,'.1f').replace('.',',')+' KB. A capacidade de pixels RGBA8 sem compressão e sem mipmaps passa de **'+format(before,'.2f').replace('.',',')+' MiB para '+format(after,'.2f').replace('.',',')+' MiB**, redução de **'+format(summary['texel_reduction_percent'],'.1f').replace('.',',')+'%**. A memória real e o tamanho do aplicativo dependem dos ajustes de import, compressão, plataforma e atlas; estes números medem os arquivos e a área das texturas.','',
        'Produção: recorte técnico lossless com `sharp`, incluindo todos os pixels de alfa não zero e a margem transparente. A ilustração original foi produzida com ImageGen integrado. O [registro desta exportação](prompts/ART-11-compact.json) conserva os caminhos, SHA256, medidas e origem; os [prompts de arte originais](prompts/ART-11.json) continuam disponíveis.','',
        '[Script de recorte](../../art-source/editable/ART-11/crop-compact.cjs) · [QA](qa/ART-11-compact.json) · [Prova pixel a pixel](qa/ART-11-compact-measurements.json) · [Auditoria](qa/ART-11-compact-scope-audit.json)','',
        'Validação: cada recorte, colocado nas coordenadas registradas, reconstrói o full-frame com **diferença zero em todos os bytes RGBA**. A montagem conjunta também é idêntica. Recortes revisados sobre marfim/ameixa e no tamanho de exibição. Geometria, câmera, zonas, labels e hit-areas do mapa continuam com o mesmo encaixe.','',
        '[Entrega full-frame preservada](ART-11-fullframe-entregas.md) · [Contrato anterior preservado](layouts/archive/map-layout-art11-fullframe.json)','',
        'Pendências desta exportação: nenhuma. A aplicação dos novos PNGs às cenas fica para a integração.']
    (ROOT/'docs/art/ART-11-entregas.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps({'delivery':'docs/art/ART-11-entregas.md','preferred_variant':'compact'}))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('mode',choices=['publish','finalize','audit','deliver']);p.add_argument('--visual-reviewed',action='store_true');args=p.parse_args()
    if args.mode=='finalize' and not args.visual_reviewed:p.error('Visual inspection must be completed first.')
    {'publish':publish,'finalize':finalize,'audit':audit,'deliver':deliver}[args.mode]()
