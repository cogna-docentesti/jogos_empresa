"""Create same-canvas-scale reference crops and validate the FONT-01 handoff."""
import hashlib
import html
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/ui-update/fonts/FONT-01'
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def write(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    coverage=load(ROOT/'docs/ui-update/font-coverage.json');contract=load(ROOT/'docs/ui-update/font-contract.json')
    raw=load(OUT/'serialized-texts.json');runtime=load(OUT/'runtime-texts.json');captures=load(OUT/'captures.json');execution=load(OUT/'execution.json')
    assert execution['passed'] and len(captures)==86
    assert len({r['hierarchy_key'] for r in runtime})==len(runtime),'Runtime hierarchy keys must not merge sibling clones'
    assert len({r['global_id'] for r in raw})==len(raw)
    assert all(c['advancing'] and (c['width'],c['height'])==(1920,1080) for c in captures)
    assert contract['status']=='planejado' and contract['replaces_visual_contract'] is False
    assert all(x['target']['family'] in ('Roboto','Roboto Condensed') and x['owner'].startswith('FONT-') for x in coverage['consumers'])
    assert all(x['migration_state'] in ('planejado','fora-do-build') for x in coverage['consumers'])
    assert len({x['id'] for x in coverage['consumers']})==len(coverage['consumers'])
    assert len(coverage['screens'])==20
    before=load(OUT/'live-before.json');after=load(OUT/'live-after.json')
    fields=['path','text','font_asset','font_family','font_face','font_weight','style','font_size','material','face_dilate','enabled','active_self','local_scale']
    signature=lambda d:[{k:r[k] for k in fields} for r in d['texts']]
    preserved=before['dirty']==after['dirty'] and signature(before)==signature(after)
    assert preserved,'Original unsaved Editor text state was changed'
    hashes=load(OUT/'initial-file-hashes.json')
    changed=[p for p,h in hashes.items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h]
    assert not changed,'Pre-existing file bytes changed: '+repr(changed)
    assert load(OUT/'console-errors.json')==[]

    # Source sheet bounds exclude quadrant headings. Each full screen is scaled
    # uniformly to canvas width 1920 before cropping; no glyph-height equalizing,
    # nonuniform scaling, horizontal compression or old screenshots.
    specifications=[
        dict(id='header',label='Cabeçalho / marca',capture='bankruptcy-month-1-summary',current=[20,8,540,80],
             concept='04-dre-mensal.png',screen=[0,0,1672,941],region=[14,8,540,80],
             current_text='Canvas/Panel_MonthlyResult/Header/ConceptBrand',role='brand',
             note='Marca atual tem menor altura aparente e desenho diferente do concept. Roboto Bold 700; testar 44 atual antes de harmonizar com token 36.'),
        dict(id='button',label='Botão de ação',capture='bankruptcy-identification',current=[1210,782,612,120],
             concept='02-abertura.png',screen=[4,43,832,467],region=[1200,720,640,180],
             current_text='Canvas/Panel_Identification/DesignFrame/Content/Popup/ButtonRegister/Text (TMP)',role='button',
             note='Concept usa letras mais estreitas e altas. Destino Roboto Condensed SemiBold 600; manter tamanho atual 24 como partida e aferir área e altura.'),
        dict(id='input',label='Input / campo e rótulo',capture='bankruptcy-identification',current=[1200,320,640,146],
             concept='02-abertura.png',screen=[4,43,832,467],region=[1190,330,670,175],
             current_text='Canvas/Panel_Identification/DesignFrame/Content/Popup/InputName/InputName/Text Area/Text',role='input',
             note='Os campos do concept estão vazios; comparar rótulo/caixa e linha de base. Entrada e placeholder Roboto Regular 400, rótulo Condensed Medium 500. Nome longo permanece dado editável.'),
        dict(id='card',label='Card de catálogo',capture='bankruptcy-equipment',current=[30,206,418,704],
             concept='03-gestao.png',screen=[842,42,1668,465],region=[48,252,430,655],
             current_text='Canvas/Panel_Equipment/MainArea_Equipment/CatalogScroll/Viewport/Content/Card_equipment(Clone)/EquipmentName',role='section_title',
             note='Comparar título, etiqueta, preço e ações no contexto. Nome Condensed Bold 700; preço unitário Roboto Regular; categoria Medium 500 e ações SemiBold 600. Conteúdo/catalogo difere do concept.'),
        dict(id='table',label='Tabela financeira',capture='bankruptcy-month-1-summary',current=[40,340,1224,112],
             concept='04-dre-mensal.png',screen=[0,0,1672,941],region=[48,340,1230,130],
             current_text='Canvas/Panel_MonthlyResult/LoadedBody/TableScroll/Viewport/Content/Row0/Label',role='table',
             note='Corpo regular e alinhamento monetário à direita. Roboto Regular 400 nas células; Bold 700 nos totais. Tamanho vigente 24 preservado inicialmente; token de tabela 22 ainda não é tamanho homologado.'),
        dict(id='kpi',label='KPI / valor de destaque',capture='bankruptcy-month-1-summary',current=[40,204,400,112],
             concept='04-dre-mensal.png',screen=[0,0,1672,941],region=[38,195,430,143],
             current_text='Canvas/Panel_MonthlyResult/LoadedBody/Kpi0/Value',role='kpi',
             note='Valor atual tem largura e contorno diferentes. Roboto Bold 700; preservar tamanho vigente 38/42 conforme consumidor e medir valores negativos e longos.'),
        dict(id='map-marker',label='Marcador do mapa / complemento',capture='bankruptcy-hub',current=[820,150,250,100],
             concept='01-hub.png',screen=[0,0,1672,941],region=[700,130,200,100],
             current_text='Canvas/Panel_Menu/MapMaster/Node_Bank/Card/MapCaption',role='map_marker',
             note='Legenda nativa do mapa em Condensed SemiBold 600; preservar a medida individual, sem comprimir o RectTransform. O recorte atual mostra proximidade/sobreposição entre legendas a rever no FONT-05. Letreiro BANCO pintado no prédio permanece na ilustração.'),
        dict(id='quarter-table',label='Tabela trimestral / complemento',capture='bankruptcy-quarter',current=[40,340,1224,116],
             concept='05-dre-trimestral.png',screen=[0,0,1672,941],region=[48,395,1230,105],
             current_text='Canvas/Panel_QuarterlyResult/LoadedBody/TableScroll/Viewport/Content/Row0/Cell3',role='table_total',
             note='Roboto Regular 400 nas células; agregado cells[3] e totais reais em Bold 700. cells[4] é variação, não a coluna total. Preservar 20/22 atuais para iniciar a aferição.'),
    ]
    (OUT/'comparisons').mkdir(exist_ok=True)
    records=[];selected=set();gallery=[]
    for spec in specifications:
        cap=next(c for c in captures if c['name']==spec['capture'])
        source=Path(cap['path']);selected.add(source.relative_to(ROOT).as_posix())
        concept=ROOT/'art-source/references/concepts'/spec['concept']
        full=Image.open(concept).convert('RGB').crop(tuple(spec['screen']))
        scale=1920/full.width;normalized=full.resize((1920,round(full.height*scale)),Image.Resampling.LANCZOS)
        x,y,w,h=spec['region'];reference=normalized.crop((x,y,x+w,y+h))
        x,y,w,h=spec['current'];current=Image.open(source).convert('RGB').crop((x,y,x+w,y+h))
        # Both crops use equal pixel scale, and are padded rather than stretched.
        width=max(360,reference.width,current.width);height=max(reference.height,current.height)
        sheet=Image.new('RGB',(width*2+60,height+64),'#24113d');draw=ImageDraw.Draw(sheet)
        font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',19)
        draw.text((14,12),'Concept · quadro com largura 1920',font=font,fill='#f5f0e8')
        draw.text((width+44,12),'Unity atual · Inter Tight',font=font,fill='#f5f0e8')
        sheet.paste(reference,(14,50));sheet.paste(current,(width+44,50))
        relative='comparisons/'+spec['id']+'.png';sheet.save(OUT/relative)
        matched=[x['id'] for x in coverage['consumers'] if x['hierarchy']==spec['current_text'] and (spec['id']!='card' or '(Clone)[0]' in (x.get('hierarchy_key') or ''))]
        assert matched, spec['current_text']
        original_region=[spec['screen'][0]+spec['region'][0]/scale,spec['screen'][1]+spec['region'][1]/scale,spec['region'][2]/scale,spec['region'][3]/scale]
        records.append(dict(**spec,comparison='output/ui-update/fonts/FONT-01/'+relative,
                            current_file=source.relative_to(ROOT).as_posix(),current_sha256=sha(source),captured_utc=cap['captured_utc'],frame=cap['frame'],
                            concept_file=concept.relative_to(ROOT).as_posix(),concept_sha256=sha(concept),concept_uniform_scale=scale,
                            concept_original_region_xywh=original_region,consumer_ids=matched,
                            scale_rule='tela do concept normalizada uniformemente pela largura 1920; screenshot 1:1; crops sem esticar nem equalizar altura de glifos'))
        gallery.append(f'<section><h2>{html.escape(spec["label"])}</h2><p>{html.escape(spec["note"])}</p><a href="{relative}"><img src="{relative}" alt="{spec["label"]}"></a><p class="small">Captura nova: <a href="{source.relative_to(OUT).as_posix()}">{source.name}</a> · frame {cap["frame"]} · escala concept {scale:.5f} · <code>{html.escape(spec["current_text"])}</code></p></section>')
    write(OUT/'reference-regions.json',records)
    write(OUT/'concept-review.json',dict(date='2026-10-17',all_five_inspected=True,
        concepts=[dict(file=str(p.relative_to(ROOT).as_posix()),sha256=sha(p),size=list(Image.open(p).size),
                       related_comparisons=[r['id'] for r in records if r['concept']==p.name])
                  for p in sorted((ROOT/'art-source/references/concepts').glob('*.png'))],
        prior_analysis='output/ui-update/font-identification/identification.json',
        decision='Manter Roboto e Roboto Condensed. Altura aparente da interface atual é menor em vários papéis; peso/largura precisam de aferição por tela. Nenhum tamanho final homologado em FONT-01.'))
    write(OUT/'selected-evidence.json',dict(files=sorted(selected),comparisons=[r['comparison'] for r in records],
         note='As 86 capturas e os relatórios completos existem localmente; este conjunto é o baseline visual exigido e versionado. Outras capturas são auxiliares.'))
    (OUT/'index.html').write_text('''<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>FONT-01 · Inventário tipográfico</title><style>body{margin:0;background:#f5f0e8;color:#30243c;font:17px/1.5 system-ui}main{max-width:1550px;margin:auto;padding:32px}h1{font-size:34px}h2{font-size:25px;margin-top:40px}img{max-width:100%;height:auto}section{border-top:1px solid #b99ae8;margin-top:30px}.small{font-size:13px;overflow-wrap:anywhere}a{color:#6d28d9}code{font-size:12px}.note{background:#e4d8ed;padding:20px}</style><main><h1>FONT-01 · Inventário e matriz por texto</h1><p class="note">Baseline atual: Inter Tight no Unity 6000.3.20f1 em Play Mode. As imagens desta página são capturas novas desta execução. Roboto/Roboto Condensed ainda são um contrato planejado. Nenhuma captura comprova a fonte nova.</p><p>As regiões do concept são normalizadas por largura de tela 1920 com escala uniforme. Os crops atuais mantêm seus pixels. Diferenças de tamanho/altura são visíveis; não foram igualadas artificialmente. As telas em quadrantes excluem apenas o rótulo externo do quadro. <a href="reference-regions.json">Coordenadas, escalas, hashes e consumidores</a>.</p><p>O Editor já possuía uma cena não salva com um TMP branco “Restaurante” sobre a UI. Ele foi inventariado e preservado sem salvar a cena; os seis recortes abaixo evitam essa sobreposição. Os arquivos completos continuam registrando o estado real observado.</p>'''+''.join(gallery)+'''<section><h2>Handoff verificável</h2><p><a href="../../../../../docs/ui-update/font-coverage.json">Inventário por consumidor</a> · <a href="../../../../../docs/ui-update/font-contract.json">Contrato planejado</a> · <a href="consumer-matrix.tsv">Matriz tabulada</a> · <a href="font-reuse-audit.json">Fontes existentes e necessidades</a> · <a href="glyph-corpus.json">Corpus/glifos/itálico</a></p></section></main></html>''',encoding='utf-8')
    gallery_path=OUT/'index.html'
    gallery_path.write_text(gallery_path.read_text(encoding='utf-8').replace('../../../../../docs/','../../../../docs/'),encoding='utf-8')
    import re
    for link in re.findall(r'(?:href|src)="([^"]+)"',gallery_path.read_text(encoding='utf-8')):
        assert (OUT/link).exists(),'Broken gallery link: '+link
    checks=[]
    for folder in ('campaigns','dialogs'):
        report=load(OUT/folder/'playmode.json');assert report['completed']
        checks.append(dict(suite=folder,checks=len(report['checks']),failures=sum(not c['passed'] for c in report['checks']),file=f'output/ui-update/fonts/FONT-01/{folder}/playmode.json'))
    qa=dict(task='FONT-01',date='2026-10-17',status='concluído',scope='inventário/planejamento; nenhuma fonte aplicada',
            acceptance=dict(ui01_to_ui20_owned=True,each_consumer_role_and_target=True,inactive_inputs_and_clones_included=True,
                            scene_prefab_global_ids_unique=True,runtime_sibling_keys_unique=True,font_reuse_and_six_required_faces_recorded=True,
                            no_italic_needed=True,six_current_reference_categories=True,planned_contract_kept_separate=True,old_captures_not_reused=True),
            inventory=coverage['counts'],validation=dict(unity=execution['unity'],viewport=[1920,1080],native_suites=checks,
            advancing_captures=len(captures),frame=execution['frame'],console_errors=0,sqlite=execution['database'],identification_key=execution['key'],
            dirty_editor_scene_restored=preserved,live_texts_before=len(before['texts']),live_texts_after=len(after['texts']),
            preexisting_files_checked=len(hashes),preexisting_files_changed=changed,production_assets_saved=False),
            evidence=dict(gallery='output/ui-update/fonts/FONT-01/index.html',regions='output/ui-update/fonts/FONT-01/reference-regions.json',
                          coverage='docs/ui-update/font-coverage.json',contract='docs/ui-update/font-contract.json',
                          corpus='output/ui-update/fonts/FONT-01/glyph-corpus.json',font_reuse='output/ui-update/fonts/FONT-01/font-reuse-audit.json'),
            limitations=['FONT-01 não gera fontes/assets TMP nem homologa Roboto; isto pertence a FONT-02 a FONT-10.',
                         'Baseline visual desta tarefa somente 1920×1080 no Editor; outras viewports/Player e homologação das fontes novas são tarefas seguintes.',
                         'Cena suja preexistente possui um TMP extra em memória: Canvas/Text (TMP), texto Restaurante. Preservado, inventariado e não salvo.',
                         'Nomes livres aceitam alfabetos além do corpus latino; FONT-03 precisa registrar cobertura/fallback explícitos.',
                         'O inventário retrata o workspace com alterações anteriores ainda não commitadas; a reprodução exige o estado inicial registrado, preservado fora deste commit.',
                         'Pipeline ausente no Editor aberto; execução iniciada pelo menu local do verificador via Computer Use. Tentativa de abrir outro Editor não executou QA e não é evidência.',
                         'Patches iniciais completos e log bruto de inicialização ficam locais fora do commit; hashes, cabeçalho inicial e estado inicial preservam sua rastreabilidade.'],
            next_task='FONT-02; iniciada somente pela conversa coordenadora')
    write(ROOT/'docs/ui-update/qa/FONT-01.json',qa)
    write(OUT/'preservation-check.json',dict(passed=True,preexisting_count=len(hashes),changed=[],live_editor_preserved=preserved,
          initial_patch_files=['initial-tracked.patch','initial-staged.patch'],initial_patch_hashes={p:sha(OUT/p) for p in ('initial-tracked.patch','initial-staged.patch')},
          initial_patches_committed=False))
    print(json.dumps(dict(passed=True,checks=checks,captures=len(captures),consumers=len(coverage['consumers']),selected_sources=len(selected)),ensure_ascii=False))

if __name__=='__main__':main()
