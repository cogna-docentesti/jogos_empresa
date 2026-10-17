"""Freeze an executable per-consumer font plan from the Unity API census.

This generates documentation and evidence only; it never edits Unity assets.
The resulting exact consumer IDs, semantic bindings and reviewed hierarchy rules
are migration inputs, not name heuristics to run in the shipping game.
"""
from collections import Counter
import hashlib
import html
import json
from pathlib import Path
import re
import struct
import unicodedata
import yaml

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/ui-update/fonts/FONT-01'
DOC = ROOT / 'docs/ui-update'
def read(name): return json.loads((OUT / name).read_text(encoding='utf-8-sig'))
def write(path, data): path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

ROLES = {
    'brand': ('Roboto',700,36), 'screen_title': ('Roboto',700,44),
    'section_title': ('Roboto Condensed',700,32), 'body': ('Roboto',400,24),
    'input': ('Roboto',400,24), 'table': ('Roboto',400,22),
    'table_total': ('Roboto',700,22), 'kpi': ('Roboto',700,42),
    'button': ('Roboto Condensed',600,24), 'navigation': ('Roboto Condensed',500,22),
    'label': ('Roboto Condensed',500,22), 'caption': ('Roboto Condensed',400,20),
    'map_marker': ('Roboto Condensed',600,None),
}
FACE_NAMES = {400:'Regular',500:'Medium',600:'SemiBold',700:'Bold'}
def target(role, size):
    family,weight,initial = ROLES[role]
    face = family.replace(' ','')+'-'+FACE_NAMES[weight]
    return dict(role=role,family=family,weight=weight,face=FACE_NAMES[weight],
                font_asset='Assets/TextMesh Pro/Fonts/Concept/SDF/'+face+'.asset',
                start_size=size,token_size=initial,final_size=None,
                size_status='tamanho atual preservado como partida; aferição da tarefa responsável',
                scale=1,synthetic_bold=False,material='neutro compartilhado da face')

PANELS = {
    'Panel_Identification':('FONT-05','UI-01'), 'Panel_Menu':('FONT-05','UI-04'),
    'Panel_Location':('FONT-05','UI-05'), 'SharedComponentsPreview':('FONT-05','UI-03'),
    'Panel_Restaurant':('FONT-06','UI-06'), 'Panel_MenuPricing':('FONT-06','UI-07'),
    'Panel_Establishment':('FONT-06','UI-08'), 'Panel_Review':('FONT-06','UI-08'),
    'Panel_Financial':('FONT-07','UI-09'), 'Panel_Equipment':('FONT-07','UI-10'),
    'Panel_Team':('FONT-07','UI-11'), 'Panel_Event':('FONT-09','UI-12'),
    'Panel_MonthlyResult':('FONT-08','UI-13'), 'Panel_QuarterlyResult':('FONT-08','UI-14'),
    'Panel_FinalReport':('FONT-08','UI-15'), 'Panel_Bankruptcy':('FONT-08','UI-16'),
    'Feedback':('FONT-09','UI-17'), 'ExitUI':('FONT-09','UI-18'),
    'MenuAccess':('FONT-05','UI-19'), 'HubHistory':('FONT-05','UI-19'),
}
PREFABS = {'Card_ProductItem':'FONT-06','Card_Generic':'FONT-06','Button_Segment':'FONT-06',
           'Card_loan':'FONT-07','Card_equipment':'FONT-07','EquipmentCartLine':'FONT-07',
           'TeamRoleCard':'FONT-07','Panel_Equipment':'FONT-07','MainArea_Equipment':'FONT-07',
           'EventOptionCard':'FONT-09'}
def owner(row):
    parts=row['path'].split('/')
    if any(p in ('ExitUI','PanelReset','NewGameDialog','CartModal','CloseMonthDialog','ContextualHelp','CreditTooltip','CatalogLoading','StateBody','Feedback','Tutorial') for p in parts):
        return 'FONT-09','UI-18' if any(p in ('ExitUI','PanelReset','NewGameDialog','CartModal','CloseMonthDialog') for p in parts) else 'UI-17'
    for p in parts:
        if p in PANELS: return PANELS[p]
    return PREFABS.get(Path(row['asset']).stem,'FONT-05'),'UI-03'

# Semantic precedence: native input slot; serialized view field; control binding;
# authored content and its full structural context; frozen token fallback.
# Every result is materialized as an exact ID and reviewed in text-review.tsv.
def role_for(row):
    path,content,size=row['path'],row['text'].strip(),row['font_size']
    if row.get('input_slot') in ('editable','placeholder'):
        return 'input','TMP_InputField.'+row['input_slot']+'; mesma face real Regular para entrada e placeholder'
    bindings=[b for b in row['bindings'] if b['component'].split('.')[-1] not in ('SharedControlAppearance','TMP_InputField')]
    for b in bindings:
        cls,field=b['component'].split('.')[-1],b['field']
        evidence=cls+'.'+field
        if cls in ('MapMenuNode','LocationAreaComponent','LocationMarkerV2'):
            return 'map_marker',evidence+'; legenda nativa de marcador no mapa'
        if re.match(r'rows\.Array\.data\[\d+\]\.',field):
            if field.endswith('.rule'): return 'caption',evidence+'; regra explicativa da agregação, não célula monetária'
            index=int(re.search(r'data\[(\d+)\]',field)[1])
            total=index in (7,9,11) or ('cells.' in field and field.endswith('[3]'))
            return ('table_total' if total else 'table'),evidence+'; totais 7/9/11 conforme MonthlyResultPresentation/QuarterlyResultPresentation e agregado trimestral cells[3]'
        if field.startswith('bars.') or field.startswith('monthLabels.'):
            return ('table_total' if field.endswith('.value') else 'caption'),evidence+'; valor financeiro do gráfico ou legenda de série/período'
        if field.endswith('.source'):
            return 'caption',evidence+'; proveniência do bloco do relatório'
        if field.endswith('.title') or field in ('stateTitle','resetTitle'):
            return 'section_title',evidence+'; cabeçalho de bloco/estado/modal'
        if field in ('screenTitle','regionText') or (field in ('title','titleText') and cls in ('MonthlyResultView','FinancialScreenView','MenuPricingScreenView')):
            return 'screen_title',evidence+'; título de tela'
        if field=='titleText' and cls=='ExitDialogController': return 'section_title',evidence+'; título do diálogo'
        if field in ('confirmLabel','discardLabel','buyButtonText'): return 'button',evidence+'; rótulo de ação'
        if field in ('revenueTitle','resultTitle'):return 'label',evidence+'; rótulo compacto de indicador'
        if field=='company' and size>=28:return 'section_title',evidence+'; nome da empresa como cabeçalho do relatório'
        if cls=='ConceptHudView':
            return ('caption' if field=='context' else 'kpi' if field in ('revenue','result','reputation') else 'label'),evidence+'; indicador do cabeçalho/hub'
        if cls=='MenuHudView': return ('body' if field=='companyValue' else 'label'),evidence+'; conteúdo do HUD legado preservado'
        if field in ('cash','net','result','revenue','reputation','score','cashValue','capitalValueText','cashValueText','resultValue','revenueValue','clientsValue','availableCashText','remainingCashValueText','cartTotalValueText','payrollText','countText','capacityText'):
            return 'kpi',evidence+'; valor resumido de caixa/resultado/capacidade'
        if field in ('openingValue','bridgeResultValue','closingValue','cashChangeValue'):
            return 'table_total',evidence+'; reconciliação entre resultado e caixa, valor financeiro destacado'
        if field in ('cartCashText','cartTotalText','cartRemainingText'):return 'table_total',evidence+'; reconciliação financeira do carrinho'
        if field in ('stepText','period','periodText','footer','footerText','footerHintText','newGameMessage','contextText','context','round','counter'):
            return 'caption',evidence+'; contexto/etapa/período/informação secundária'
        if field=='detailZoneNameText': return 'section_title',evidence+'; título da área selecionada'
        if cls=='EventScreenView' and field=='titleText': return 'section_title',evidence+'; título do evento, subordinado ao título de tela'
        if cls in ('BankCardView','EquipmentCardView','TeamRoleCardView','ProductPricingItemView','EventOptionCardView'):
            if field in ('nameText','creditLineName','productNameText','titleText'): return 'section_title',evidence+'; título compacto do card'
            if field in ('descriptionText','requirementText','effectsText','errorText'): return 'body',evidence+'; descrição/efeito/validação do card'
            if field in ('categoryText','statusText','availabilityText','editingText'):return 'label',evidence+'; categoria/estado compacto do card'
            return 'table',evidence+'; dado unitário do catálogo/quantidade/preço; não é total financeiro'
        if field.startswith('summaryPrices.'): return 'table',evidence+'; preço no resumo do cardápio'
        if field.startswith('summaryNames.'): return 'body',evidence+'; nome do item no resumo'
        return 'body',evidence+'; conteúdo informativo/explicativo vinculado à view'
    if content=='Jogos de Empresa': return 'brand','conteúdo da marca e contexto de cabeçalho; mesmo papel em todos os cabeçalhos duplicados/inativos'
    if row['path'].startswith(('Card_Generic/','Card_Generic(Clone)/')):
        return ('section_title' if path.endswith('/RestaurantName') else 'body'),'conteúdo do prefab de restaurante: título compacto ou descrição'
    if row['path'].startswith('Button_Segment/'):
        return 'navigation','rótulo do seletor de segmento, não ação de confirmação'
    if re.search(r'/Card_(Podrao|Japones|Frances)/',path):
        leaf=path.split('/')[-1]
        if leaf=='RestaurantName':return 'section_title','nome do restaurante no card selecionável; não confundir o conteúdo do card com sua ação'
        if leaf in ('RestaurantDesc',):return 'body','descrição da proposta do card do restaurante'
        if leaf in ('MonthlyCost','TicketRange'):return 'table','dado monetário do card do restaurante'
        return 'caption','metadado/estado/rótulo auxiliar do card do restaurante'
    if re.search(r'/(Filter\d+|Tab\d+|Tabs|Filters|Categories)(/|$)',path):return 'navigation','filtro/aba de navegação na hierarquia de seleção do catálogo'
    if '/Summary/' in path and path.endswith(('/Cash','/Total','/Remaining')):return 'kpi','indicador financeiro no resumo do carrinho, inclusive template inativo'
    if '/CartModal/' in path and path.endswith('/Title'):return 'section_title','cabeçalho do modal de compra; subordinado à tela'
    if any(b['component'].endswith('.SharedControlAppearance') for b in row['bindings']):
        if any(s in path for s in ('/Tab','/Tabs/','/Filters/','/Categories/','/Radio','/Checkbox','/Toggle','/Pill')):
            return 'navigation','SharedControlAppearance.labels em seletor/filtro/aba; contexto completo da hierarquia'
        return 'button','SharedControlAppearance.labels em controle de ação nativo'
    if '/MapMaster/' in path or '/Node_' in path or '/Markers/' in path:
        return 'map_marker','contexto estrutural do marcador/atalho do mapa; manter tamanho atual específico'
    if re.search(r'/(?:Button|Confirm|Cancel|Close|Back|Retry|Add|Remove|Increase|Decrease|Maximum|Buy)(?:/|[^/]*Button/)',path) or content in ('+','−'):
        return 'button','texto de ação/controle +/- no contexto do card ou botão'
    if '/Table/' in path or '/TableScroll/' in path or '/Columns/' in path:
        return ('table_total' if 'Bold' in row['style'] else 'table'),'contexto de tabela/linha financeira; peso forte apenas em subtotal/total existente'
    if size>=42:
        return ('kpi' if re.search(r'(R\$|\d)',content) and not content.startswith(('PASSO','ETAPA')) else 'screen_title'),'conteúdo e caixa de título/valor principal na composição; tamanho existente registrado separadamente'
    if size>=28:
        return ('table_total' if content.startswith(('R$','+ R$','− R$')) else 'section_title'),'título de seção/card ou total financeiro, conferido no contexto e no conteúdo'
    if size<=20 or content.startswith(('PASSO','ETAPA','REVISÃO ·')):
        return 'caption','texto secundário curto/etapa/nota na composição; parte do tamanho vigente sem redução global'
    if size<=22 or '/Label' in path or '/Caption' in path:
        return 'label','rótulo compacto no contexto de formulário, metadado ou filtro; texto corrido permanece corpo'
    return 'body','texto informativo/campo de conteúdo na hierarquia inventariada; Roboto Regular, preservando a caixa e o tamanho inicial'

def ttf_metadata(path):
    data=path.read_bytes();count=struct.unpack_from('>H',data,4)[0];tables={}
    for i in range(count):
        tag,_,offset,length=struct.unpack_from('>4sIII',data,12+16*i);tables[tag.decode()]=(offset,length)
    names={};off,_=tables['name'];_,cnt,st=struct.unpack_from('>HHH',data,off)
    for i in range(cnt):
        platform,enc,lang,nid,length,start=struct.unpack_from('>6H',data,off+6+12*i)
        if nid not in (1,2,5,6,16,17):continue
        raw=data[off+st+start:off+st+start+length]
        value=raw.decode('utf-16-be' if platform in (0,3) else 'mac_roman',errors='replace')
        names.setdefault(str(nid),value)
    axes=[]
    if 'fvar' in tables:
        off,_=tables['fvar'];start=struct.unpack_from('>H',data,off+4)[0];cnt,sz=struct.unpack_from('>HH',data,off+8)
        for i in range(cnt):
            tag,low,default,high=struct.unpack_from('>4siii',data,off+start+i*sz)
            axes.append(dict(axis=tag.decode(),min=low/65536,default=default/65536,max=high/65536))
    return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path),names=names,axes=axes)

def main():
    raw=read('serialized-texts.json'); runtime=read('runtime-texts.json')
    saved_paths={x['path'] for x in raw if x['scope']=='production-scene'}
    live_extras=[x for x in read('live-before.json')['texts'] if x['path'] not in saved_paths]
    consumers=[]
    def add(row,kind,index):
        role,reason=role_for(row); task,ui=owner(row)
        exception=None
        if row['scope']=='scene-outside-build':exception='cena antiga fora do build; preservar até decisão explícita de reativação'
        elif row['scope']=='test-prefab':exception='fixture de teste fora da interface de produção'
        elif '/SharedComponentsPreview/' in row['path']:exception='preview inativo de componentes; referência de FONT-05, sem rota de jogador'
        if kind=='runtime' and row['path'] in {x['path'] for x in live_extras}:
            exception='texto preexistente apenas na cena suja do Editor (não salvo no disco); preservado. Não serializar automaticamente durante a migração.'
        ident=hashlib.sha256((row['asset']+'|'+row.get('hierarchy_key',row['path'])+'|'+(row['global_id'] if kind=='serialized' else kind)).encode()).hexdigest()[:16]
        consumers.append(dict(id=ident,kind=kind,asset=row['asset'],hierarchy=row['path'],hierarchy_key=row.get('hierarchy_key'),
                              global_id=row['global_id'],scope=row['scope'],component=row['component'],active_self=row['active_self'],
                              active_in_hierarchy=row['active_in_hierarchy'],enabled=row['enabled'],text=row['text'],
                              bindings=row['bindings'],input_slot=row['input_slot'],
                              current={k:row[k] for k in ('font_asset','font_family','font_face','font_weight','style','material','material_name','face_dilate','font_size','autosize','rich_text','overflow','local_scale')},
                              target=target(role,row['font_size']),role_evidence=reason,owner=task,ui=ui,
                              provider_owner='FONT-04',migration_state='fora-do-build' if row['scope']=='scene-outside-build' else 'planejado',
                              exception=exception,raw_evidence=dict(file='output/ui-update/fonts/FONT-01/'+('serialized-texts.json' if kind=='serialized' else 'runtime-texts.json'),index=index),
                              runtime_samples=row.get('samples',[]),observed_in=sorted({x['capture'] for x in row.get('observed_at',[])})))
    for i,row in enumerate(raw):add(row,'serialized',i)
    for i,row in enumerate(runtime):add(row,'runtime',i)

    sources=[]; corpus=[]; tags=Counter(); strings=[]
    pattern=re.compile(r'TMP_Text|TextMeshPro|TMP_FontAsset|\.font\b|\.fontSize\b|\.fontStyle\b|\.fontWeight\b|\.fontSharedMaterial\b|GameTypography\.|FontGuid|FontGUID|FontPath|FontAssetPath|InterTight|Inter Tight|boldStyle|boldSpacing|_FaceDilate|FontWeight|FontStyles|LineSpacing|CharacterSpacing|\.text\s*=|\.SetText\(|Instantiate\(|OnEnable\(')
    roots=[ROOT/'Assets/Scripts/Game',ROOT/'Assets/Editor/UI',ROOT/'Assets/Editor/Tests']
    for folder in roots:
        for path in sorted(folder.rglob('*.cs')):
            if path.name=='Font01InventoryValidation.cs':continue
            rel=path.relative_to(ROOT).as_posix(); lines=path.read_text(encoding='utf-8-sig').splitlines()
            for number,line in enumerate(lines,1):
                metric_comment=line.strip().startswith(('//','///','*')) and bool(re.search(r'InterTight|Inter Tight|cap/em|line height',line,re.I))
                if pattern.search(line) and (not line.strip().startswith(('//','///','*')) or metric_comment):
                    kind='verification' if '/Tests/' in rel or '/TESTES/' in rel else 'editor-provider' if '/Editor/' in rel else 'runtime-provider'
                    action='metric-assumption' if metric_comment else 'style-assignment' if re.search(r'font(?:Style|Weight|SharedMaterial|Size)?\s*=|GameTypography.Apply|FontGuid|FontGUID|FontPath|FontAssetPath|boldStyle|boldSpacing',line) else 'creation-or-clone' if re.search(r'AddComponent<.*Text|Instantiate\(|TMP_FontAsset',line) else 'content-or-lifecycle'
                    sources.append(dict(id=rel+':'+str(number),asset=rel,line=number,code=line.strip(),kind=kind,action=action,
                                        owner='FONT-04',consumer_owners=sorted({x['owner'] for x in consumers if any(b['component'].endswith('.'+path.stem) for b in x['bindings'])}),
                                        migration_state='planejado',target='resolver papel/família/peso/material pelo contrato; estilos não podem repor Inter Tight',
                                        exact_consumer_ids=[x['id'] for x in consumers if x['kind']=='serialized' and any(b['component'].endswith('.'+path.stem) for b in x['bindings'])]))
            if '/Tests/' not in rel and '/TESTES/' not in rel:
                for m in re.finditer(r'(?<!\\)"((?:\\.|[^"\\])*)"', '\n'.join(lines)):
                    value=m.group(1)
                    if '/' in value and value.startswith(('Assets/','shared/','hud/')):continue
                    value=re.sub(r'\\u([0-9a-fA-F]{4})',lambda m:chr(int(m[1],16)),value)
                    value=value.replace('\\n','\n').replace('\\r','\r').replace('\\t','\t').replace('\\"','"')
                    strings.append(value)
                    tags.update(re.findall(r'</?([a-zA-Z][\w-]*)(?:=[^>]*)?>',value))
    for row in raw+runtime:
        corpus.append(row['text']);corpus.extend(row.get('samples',[]));tags.update(re.findall(r'</?([a-zA-Z][\w-]*)(?:=[^>]*)?>',row['text']))
    content_sources=[]
    for path in sorted((ROOT/'Assets/Resources').rglob('*.asset')):
        source=path.read_text(encoding='utf-8-sig')
        source=re.sub(r'^%.*\n','',source,flags=re.M)
        source=re.sub(r'^--- !u!\d+ &[^\n]+','---',source,flags=re.M)
        data=yaml.safe_load(source)
        def visit(value,prop=''):
            if isinstance(value,dict):
                for key,item in value.items():
                    if key.startswith('m_'):continue
                    field=prop+'.'+key if prop else key
                    if isinstance(item,str) and item.strip() and key in ('displayName','description','cardDescription','title','subtitle','highlight','educationalConcept','label','body','text','guidance'):
                        strings.append(item)
                        resource=path.relative_to(ROOT).as_posix();category=path.parent.name
                        task={'Events':'FONT-09','Locations':'FONT-05','Restaurants':'FONT-06','Products':'FONT-06','Roles':'FONT-07','Equipments':'FONT-07','CreditLines':'FONT-07'}.get(category,'FONT-04')
                        content_sources.append(dict(asset=resource,property=field,text=item,owner=task,
                                                    role='section_title' if key in ('title','displayName') else 'body',
                                                    usage='conteúdo do catálogo; consumidores de view/template têm IDs e destinos próprios no inventário',
                                                    evidence='YAML decodificado por PyYAML '+yaml.__version__))
                    elif isinstance(item,(dict,list)):visit(item,field)
            elif isinstance(value,list):
                for i,item in enumerate(value):visit(item,prop+'['+str(i)+']')
        visit(data.get('MonoBehaviour',{}))
    tags.pop('Keyboard',None) # Input System binding path, not TMP rich text.
    raw_corpus='\n'.join(corpus+strings)
    content=re.sub(r'<[^>]+>','',raw_corpus)
    required='ÁÉÍÓÚ ÂÊÔ ÃÕ Ç áéíóú âêô ãõ ç R$ -12.345,67 72 / 100 + − % → ← ↑ ↓ • · – — … º ª'
    actual=sorted({ord(c) for c in content if unicodedata.category(c)[0]!='C'})
    primary=sorted(set(actual)|{ord(c) for c in required}|set(range(32,127))|{x for x in range(160,592) if unicodedata.category(chr(x))[0]!='C'})
    (OUT/'corpus.txt').write_text('\n'.join(line.rstrip() for line in (content+'\n'+required).splitlines()).rstrip()+'\n',encoding='utf-8')
    (OUT/'primary-characters.txt').write_text(''.join(map(chr,primary)),encoding='utf-8')
    write(OUT/'glyph-corpus.json',dict(status='insumo FONT-02/FONT-03',actual_characters=len(actual),primary_characters=len(primary),
         actual_codepoints=[f'U+{x:04X}' for x in actual],primary_codepoints=primary,required_examples=required,
         sources=dict(serialized_and_runtime_texts=True,script_literals='Assets/Scripts/Game + Assets/Editor/UI, inclui chaves técnicas e formatos como superset conservador',resource_assets=True),
         rich_text_tags=dict(tags),italic_consumers=sum('Italic' in x['style'] for x in raw+runtime),
         italic_required=False,italic_reason='Nenhum TMP serializado/runtime em Italic e nenhum emissor de <i> ou FontStyles.Italic na UI de produção.',
         name_input_policy='Campos Standard aceitam texto livre: cobrir ASCII/Latin-1/Latin Extended A+B no atlas primário. Outros alfabetos não têm cobertura garantida; FONT-03 deve registrar fallback explícito sem retorno silencioso à Inter Tight.',
         controls=[f'U+{ord(c):04X}' for c in sorted(set(content)) if unicodedata.category(c)[0]=='C'],
         note='Zero width space de input vazio, tab/newline são controles, não glifos visíveis. Não interpretar chaves de formato como conteúdo final.'))

    fonts=read('font-assets.json')
    research=[ttf_metadata(p) for p in sorted((ROOT/'output/ui-update/font-identification').glob('Roboto*.ttf'))]
    examples=ttf_metadata(ROOT/'Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf')
    write(OUT/'font-reuse-audit.json',dict(existing_tmp=fonts,research_sources=research,example_source=examples,
        production_reusable_primary_assets=[],
        findings=['GameScene salvo: todos os 1051 TMP referenciam Inter Tight; não existe primário Roboto/Condensed integrado.',
                  'Roboto-Bold SDF de Examples & Extras é Dynamic, contém somente os glifos já observados do sample e não possui matriz de seis faces; não reutilizar como asset de produção.',
                  'Inter Tight normal/itálico e Liberation Sans preservados para cenas antigas/samples ou fallback existente; não aprovados como primários novos.',
                  'SharedUiSkin e LocationSkinV2 contêm sprites/cores, não TMP_FontAsset. LocationMockupTokens contém métricas/tokens vinculados a GameTypography.',
                  'Roboto e Roboto Condensed de output são fontes variáveis de pesquisa; hashes e eixos lidos do binário. FONT-02 precisa verificar revisão, origem e licença e gerar faces estáticas no projeto.'],
        font02_required=['Roboto Regular 400','Roboto Bold 700','Roboto Condensed Regular 400','Roboto Condensed Medium 500','Roboto Condensed SemiBold 600','Roboto Condensed Bold 700'],
        font03_required='Seis TMP primários estáticos completos, material neutro/atlas por face, escala 1, corpus testado; fallback explícito limitado. Itálicos dispensados no conteúdo encontrado.'))

    excluded=[p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets').rglob('*') if p.suffix in ('.unity','.prefab') and 'TextMesh Pro/Examples & Extras' in p.as_posix() or p.suffix=='.unity' and 'Layer Lab' in p.as_posix()]
    screens=[]
    for i in range(1,21):
        ui=f'UI-{i:02}'
        own={1:'FONT-05',2:'FONT-04',3:'FONT-05',4:'FONT-05',5:'FONT-05',6:'FONT-06',7:'FONT-06',8:'FONT-06',9:'FONT-07',10:'FONT-07',11:'FONT-07',12:'FONT-09',13:'FONT-08',14:'FONT-08',15:'FONT-08',16:'FONT-08',17:'FONT-09',18:'FONT-09',19:'FONT-04',20:'FONT-10'}[i]
        screens.append(dict(ui=ui,owner=own,consumer_ids=[x['id'] for x in consumers if x['ui']==ui],
                            shared_scope={2:'Tema/factories/fontes: source_consumers',19:'Navegação/reentrada atravessa todos os consumidores; controls/HUD FONT-05; diálogos FONT-09',20:'Validação transversal final de todos os textos, glifos e resoluções'}.get(i)))
    illustrations=[dict(scope='Assets/Art e art-source/references/concepts',decision='preservar texto pintado na ilustração; fora da troca TMP',examples=['fachadas e letreiros RESTAURANTE','quadros de cozinha e papéis do escritório','texto gerado dentro dos cinco concepts'],owner='FONT-05 a FONT-09 conforme tela')]
    coverage=dict(schema_version=1,task='FONT-01',status='inventariado; migração planejada',date='2026-10-17',
                  production_scene='Assets/Scenes/GameScene.unity',build_scenes=['Assets/Scenes/GameScene.unity'],
                  provenance=dict(unity='6000.3.20f1',raw='output/ui-update/fonts/FONT-01',scripts=['tools/ui/build_font01_reports.py','Assets/Editor/Tests/Font01InventoryValidation.cs'],
                                  old_captures_used_as_new_evidence=False,scene_saved_by_task=False),
                  counts=dict(serialized=len(raw),runtime_hierarchy_slots=len(runtime),consumers=len(consumers),roles=dict(Counter(x['target']['role'] for x in consumers)),source_consumers=len(sources),
                              text_3d=sum(x['component']=='TMPro.TextMeshPro' for x in raw),inactive_self=sum(not x['active_self'] for x in raw)),
                  scope_rules=['Inclui todos os TMP de Assets/Scenes, Assets/Settings/Scenes, Assets/Prefabs e fixture TESTES; inclusive desativados e slots de input.',
                               'Samples de terceiros fora do build listados separadamente; seus TMP não são a UI do jogo.',
                               'Caminhos podem repetir em hierarquias antigas; global_id identifica univocamente cada texto serializado. hierarchy_key distingue índices de irmãos no runtime.',
                               'Mapeamento congelado por consumidor com binding/caminho/conteúdo/tamanho para auditoria; não implementar uma heurística por nome de GameObject.'],
                  screens=screens,consumers=consumers,source_consumers=sources,scanned_assets=read('scanned-assets.json'),excluded_third_party_samples=sorted(excluded),
                  illustration_exceptions=illustrations,font_reuse='output/ui-update/fonts/FONT-01/font-reuse-audit.json',glyph_corpus='output/ui-update/fonts/FONT-01/glyph-corpus.json')
    coverage['live_editor_extras']=[dict(path=x['path'],text=x['text'],font=x['font_asset'],decision='inventariado no runtime; preservar alteração em memória do usuário',owner='FONT-05') for x in live_extras]
    coverage['content_sources']=content_sources
    coverage['global_font_providers']=[
        dict(asset='Assets/TextMesh Pro/Resources/TMP Settings.asset',property='m_defaultFontAsset',guid='8f586378b4e144a9851e7b34d9b748ee',
             current='Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset',owner='FONT-04',
             target='Roboto Regular 400 como default explícito quando aplicável; evitar que criação sem papel retorne a Liberation Sans. Rever impacto em samples e preservar referência legítima.',migration_state='planejado'),
        dict(asset='Assets/Scripts/Game/Adapter/In/UI/Theme/SharedUiSkin.cs',current='somente sprites, sem campos de fonte',owner='FONT-04',target='não adicionar uma segunda fonte de verdade paralela ao resolvedor de papéis'),
        dict(asset='Assets/Scripts/Game/Adapter/In/UI/Theme/LocationSkinV2.cs',current='somente sprites e cores; sem campos de fonte',owner='FONT-04',target='preservar skin e centralizar fontes no resolvedor'),
        dict(asset='Assets/Scripts/Game/Adapter/In/UI/Theme/LocationMockupTokens.cs',current='tokens de tamanho e comentários métricos da Inter Tight',owner='FONT-04',target='compatibilidade por papel; aferir métricas de Roboto/Roboto Condensed')]
    write(DOC/'font-coverage.json',coverage)
    contract=dict(schema_version=1,status='planejado',task='FONT-01',date='2026-10-17',replaces_visual_contract=False,
                  vigente='docs/ui-update/visual-contract.json',consumer_map='docs/ui-update/font-coverage.json',
                  base_frame=[1920,1080],decision='Roboto e Roboto Condensed são aproximações dos concepts; não identificação da fonte original.',
                  roles={r:target(r,v[2]) for r,v in ROLES.items()},
                  implementation=dict(resolution='papel explícito → família/peso/asset/material/tamanho; preservar API antiga durante FONT-04',
                                      runtime='referências serializadas ou Resources; AssetDatabase somente no Editor; sem fonte de sistema/output',
                                      neutral_material=dict(face_dilate=0,outline=0,underlay=False),
                                      weights='face real; evitar FontStyles.Bold/fontWeight/<b> cumulativos. Não há itálico usado no conteúdo atual.',
                                      spacing='partir de 0 e medir altura/contorno real; escala da face e do texto = 1',
                                      sizing='start_size de cada consumidor é vigente; token_size é referência inicial. final_size só após QA da tela, sem reduzir globalmente/truncar para ocultar overflow.',
                                      primary_atlas=dict(population='Static',sampling_start=80,padding_start=8,atlas_start=[1024,1024],face_scale=1),
                                      fallback='mesma família quando possível, atlas dinâmico 512–1024 limitado/limpo no build, sem ciclos. Alfabeto fora do corpus precisa de política explícita.'),
                  exceptions=[dict(role='brand',reason='ConceptBrand atual 44 e GameTitle/Brand atual 36: mesma Roboto Bold, tamanhos atuais preservados inicialmente; harmonizar FONT-05.',final_size=None),
                              dict(role='screen_title',reason='Identificação atual 56 e trimestral 46; manter como partida e aferir, token 44 não é ordem de redução.',final_size=None),
                              dict(role='map_marker',reason='Roboto Condensed SemiBold 600; tamanho vigente por marcador, não aplicar globalmente 24.',final_size=None),
                              dict(role='input',reason='Card_ProductItem/PriceInput está Bold hoje; destino Regular 400 porque é campo editável, mesmo quando mostra moeda.',final_size=None),
                              dict(role='table_total',reason='Valores do CashBridge atuais 28/24 e subtotal/total em 22: conservar tamanho vigente para iniciar; face real Bold sem espessura sintética.',final_size=None)],
                  sources_to_acquire='output/ui-update/fonts/FONT-01/font-reuse-audit.json',glyphs='output/ui-update/fonts/FONT-01/glyph-corpus.json',
                  acceptance='Contrato planejado. Nenhum consumidor pode ser marcado migrado por FONT-01; aplicação FONT-04 a FONT-09 e homologação final FONT-10.')
    write(DOC/'font-contract.json',contract)
    (OUT/'consumer-matrix.tsv').write_text('id\tkind\tasset\thierarchy\trole\tfamily\tweight\tstart_size\towner\tstate\n'+'\n'.join('\t'.join(map(str,[x['id'],x['kind'],x['asset'],x['hierarchy'],x['target']['role'],x['target']['family'],x['target']['weight'],x['target']['start_size'],x['owner'],x['migration_state']])) for x in consumers)+'\n',encoding='utf-8')
    print(json.dumps(coverage['counts'],ensure_ascii=False))

if __name__=='__main__':main()
