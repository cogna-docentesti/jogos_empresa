"""Historical bootstrap; the live manifest is maintained in docs/art."""
raise SystemExit("Arquivo histórico: não executar. Use docs/art/manifest.json e tools/art/validate_assets.py.")

from pathlib import Path
import json, shutil, html, collections, sys

sys.stdout.reconfigure(encoding='utf-8')
PROJECT = Path(r'D:\Cogna\jogos_empresa')
ROOT = PROJECT / 'Assets/Art/new'
PROD = PROJECT / 'docs/art'
CAT = json.loads((PROJECT/'output/art-production-audit/2026-10-14/catalogos.json').read_text(encoding='utf-8'))
ROOT.mkdir(parents=True, exist_ok=True)
PROD.mkdir(parents=True, exist_ok=True)
REF = PROJECT/'art-source/references/concepts'
REF.mkdir(parents=True, exist_ok=True)
generated = Path(r'C:\Users\Pichau\.codex\generated_images\01a1376a-c564-7a32-9c0f-63d6e89ec38c')
refs = {
 '01-hub.png':'exec-ec7aaea4-579e-480c-8390-ce2332c67db9.png',
 '02-abertura.png':'exec-b003adde-7d1f-4729-a9f0-3ded95048e4a.png',
 '03-gestao.png':'exec-0e8fe361-6d4d-4a8b-bb3c-c35135e9ed69.png',
 '04-dre-mensal.png':'exec-b4505d0f-204e-48eb-89aa-99b446fa54d0.png',
 '05-dre-trimestral.png':'exec-d1a45101-632c-4721-a120-d83161daa7be.png'
}
for dest, source in refs.items():
    if not (REF/dest).exists(): shutil.copy2(generated/source, REF/dest)

characters = [
 {'id':'CH01','name':'Profissional de camisa lilás','origin':'03-gestao.png e 04-dre-mensal.png','roles':['attendant','guia','análise de resultados'],'appearance':'Adulta, pele morena média, olhos castanhos, cabelo castanho escuro ondulado em bob acima dos ombros, risca lateral, argolas douradas, camisa lilás de manga dobrada, calça grafite; prancheta quando a pose pede. Preservar rosto e silhueta em identificação, banco, atendimento e DRE.'},
 {'id':'CH02','name':'Chapeiro','origin':'03-gestao.png','roles':['grill_cook'],'appearance':'Homem adulto moreno, cabelo escuro sob boné grafite, barba curta bem definida, camiseta branca e avental grafite. Proporções adultas, corpo e rosto iguais em card e cenas de cozinha.'},
 {'id':'CH03','name':'Gerente','origin':'03-gestao.png','roles':['manager'],'appearance':'Mulher adulta morena, cabelo castanho escuro longo e ondulado, blazer ameixa sobre blusa clara, argolas discretas. Não confundir com CH01: comprimento do cabelo, formato do rosto e figurino diferentes.'},
 {'id':'CH04','name':'Mentor de óculos','origin':'05-dre-trimestral.png','roles':['mentor','interlocutor na análise'],'appearance':'Homem adulto, cabelo castanho ondulado, óculos de armação escura, barba curta, roupa azul-marinho com camisa lilás clara. Preservar rosto, óculos, cabelo e barba em todas as aparições.'},
 {'id':'CH05','name':'Chef de cozinha','origin':'Novo personagem a definir no ART-02','roles':['head_chef'],'appearance':'Proposta inicial: mulher adulta de cerca de 40 anos, pele negra, cabelo crespo preso, dólmã clara e avental ameixa. Registrar aparência final a partir do primeiro master; não regenerar identidade em outras tarefas.'},
 {'id':'CH06','name':'Profissional de sushi','origin':'Novo personagem a definir no ART-02','roles':['sushi_chef'],'appearance':'Proposta inicial: homem adulto de cerca de 40 anos, cabelo preto curto, rosto distinto de CH02/CH04, jaqueta de cozinha clara e avental grafite com detalhe lilás. Sem caricatura. Registrar aparência final a partir do primeiro master.'}
]
for c in characters:
    c.update({'canonical_asset':f"characters/{c['id']}/{c['id'].lower()}_canonical.png",'status':'planejado','identity_hash':None,'allowed_changes':['pose','expressão natural','objeto segurado previsto'], 'locked_features':['rosto','tom de pele','cor dos olhos','penteado','figurino base','acessórios de identidade','proporção corporal']})

tasks=[]
assets=[]
def task(n,title,deps=(),scope='nucleo',note=''):
    t={'id':f'ART-{n:02}','title':title,'dependencies':[f'ART-{d:02}' for d in deps], 'scope':scope,'status':'planejado','note':note,'asset_ids':[]}
    tasks.append(t)
    return t
def asset(t,aid,path,size,display,kind,brief,alpha='recorte',slice_px=0,catalog_id=None,source=None,characters_used=(),refs_used=('01-hub.png',),scope=None):
    a={'id':aid,'task_id':t['id'],'path':path,'name':brief.split('.')[0],'kind':kind,'status':'planejado','scope':scope or t['scope'],
       'export_size_px':list(size),'display_size_px':list(display),'alpha_policy':alpha,'format':'PNG RGBA 8-bit sRGB','nine_slice_lbrt_px':[slice_px]*4,
       'pivot':[0.5,0.5],'padding_percent':10 if kind in ['personagem','equipamento','produto','icone','credito'] else 0,
       'character_ids':list(characters_used),'catalog_id':catalog_id,'source':source,'references':[f'art-source/references/concepts/{r}' for r in refs_used],
       'brief':brief,'sha256':None,'actual_size_px':None,'generation_prompt':None,'source_editable':None,
       'display_fit':'sliced' if slice_px else ('uniform_map_transform' if kind in ['camada_mapa','overlay'] else ('cover' if alpha=='rgba_opaco' and kind=='cenario' else ('exact' if kind=='componente' and alpha=='rgba_opaco' else 'contain'))),
       'qa':{'technical':False,'visual':False,'identity':False}}
    if kind in ['componente','icone']:
        a['production_method']='vector_or_code'
        a['source_editable']='art-source/editable/'+str(Path(path).with_suffix('.svg')).replace('\\','/')
    assets.append(a);t['asset_ids'].append(aid)
    (ROOT/path).parent.mkdir(parents=True,exist_ok=True)
    return a
def component(t,name,folder='shared/components',size=(256,256),display=(560,400),slice_px=24,brief=None,alpha='recorte'):
    return asset(t,'UI_'+name.upper(),f'{folder}/{name}.png',size,display,'componente',brief or f'{name}: apenas fundo/contorno, sem ícone, label ou sombra incorporada.',alpha,slice_px)

t=task(1,'Fixar os quatro personagens já aprovados',note='Uma identidade por arquivo, sem prancha coletiva como sprite final. Os masters canônicos são a referência das próximas poses.')
for c in characters[:4]:
    asset(t,c['id']+'_CANONICAL',c['canonical_asset'],(1024,1536),(420,760),'personagem',c['appearance']+' Corpo inteiro, pose neutra em três quartos, mãos e cabeça sem cortes.',characters_used=(c['id'],),refs_used=(c['origin'].split(' e ')[0],))
t=task(2,'Fixar chef e profissional de sushi',(1,),note='São as duas funções confirmadas ainda sem pessoa definida nos concepts; aparência inicial registrada no manifesto após QA.')
for c in characters[4:]:
    asset(t,c['id']+'_CANONICAL',c['canonical_asset'],(1024,1536),(420,760),'personagem',c['appearance']+' Corpo inteiro em pose neutra profissional.',characters_used=(c['id'],),refs_used=('03-gestao.png',))

t=task(3,'Barras, painéis e bases compartilhadas',note='Geometria determinística com fonte vetorial/editável; exportar cada PNG separado. Não gerar uma tela completa.')
for name,sz,ds,slice_px,alpha,folder in [
 ('topbar_violet',(1920,88),(1920,88),0,'rgba_opaco','hud/bars'),('navbar_plum',(1920,96),(1920,96),0,'rgba_opaco','hud/bars'),
 ('panel_ivory',(256,256),(568,848),24,'recorte','shared/components/panels'),('panel_lavender',(256,256),(568,848),24,'recorte','shared/components/panels'),
 ('panel_plum',(256,256),(568,848),24,'recorte','shared/components/panels'),('panel_shadow',(256,256),(580,860),32,'recorte','shared/components/panels'),
 ('card_normal',(256,256),(360,430),24,'recorte','shared/components/cards'),('card_selected_outline',(256,256),(360,430),24,'contorno','shared/components/cards'),
 ('card_disabled_overlay',(256,256),(360,430),24,'semitransparente','shared/components/cards'),('modal_surface',(256,256),(900,440),24,'recorte','shared/components/panels'),
 ('tooltip_surface',(256,128),(420,160),20,'recorte','shared/components/feedback'),('scrim',(1,1),(1920,1080),0,'semitransparente','shared/components/panels'),
 ('badge_circle',(128,128),(64,64),0,'recorte','shared/components/badges')]:
    component(t,name,folder,sz,ds,slice_px,alpha=alpha)
logo=asset(t,'BRAND_COGNA_LOGO','shared/brand/logo_cogna.png',(512,512),(120,120),'marca_reutilizada','Reutilizar o logo oficial existente sem redesenhar, recolorir ou reinterpretar por IA. Exportar o mesmo desenho em PNG RGBA preservando a transparência original; nenhuma moldura ou slogan.',source='Assets/Art/Sprites/Identification/logo-cogna.png')
logo['production_method']='copy_existing_visual_without_redesign'

t=task(4,'Botões sem texto e seus estados',(3,))
for family in ['primary_violet','secondary_ivory','danger_coral','accent_amber','icon_button']:
    for state in ['normal','hover','pressed','disabled']:
        component(t,f'{family}_{state}','shared/components/buttons',(256,128) if family!='icon_button' else (128,128),(360,72) if family!='icon_button' else (64,64),24 if family!='icon_button' else 16,brief=f'Botão {family}, estado {state}. Mesmos bounds e pivot entre estados; texto/ícone em arquivo separado.')

t=task(5,'Controles, campos e seleção',(3,4))
for family,states in [('input',['normal','focus','invalid','disabled']),('tab',['normal','selected','hover','pressed','disabled']),('radio',['off','on','disabled']),('checkbox',['off','on','disabled']),('toggle',['off','on','disabled'])]:
    for state in states:
        is_small=family in ['radio','checkbox']
        component(t,f'{family}_{state}','shared/components/controls',(64,64) if is_small else (256,128),(32,32) if is_small else ((560,80) if family=='input' else (180,52)),0 if is_small else 20)
for name,sz,ds,b in [('slider_track',(128,64),(680,16),6),('slider_fill',(128,64),(680,16),6),('slider_thumb',(64,64),(40,40),0),('stepper_base',(256,128),(200,52),20),('scrollbar_track',(64,128),(20,640),4),('scrollbar_thumb',(64,128),(16,140),3)]:
    component(t,name,'shared/components/controls',sz,ds,b)

icon_groups={
 6:('Ícones de navegação e HUD',['home','restaurant','pin','university','bank','commerce','residential','corporate','team','menu_food','equipment','document','calendar','star','cash','chart_bars','settings','cart']),
 7:('Ícones financeiros e indicadores',['revenue','trend_up','trend_down','clients','client','coherence','reputation','rent','capacity','competition','input_costs','utilities','payroll','interest','term','loan_principal','inventory','credit_contract']),
 8:('Ícones de ações, status e categorias',['back','next','chevron_up','chevron_down','close','menu','plus','minus','check','lock','alert','info','help','save','exit','reset','download','play','pause','filter','edit','trash','sync','offline','clock','search','sound_on','sound_off','fullscreen','kitchen','refrigeration','service','furniture'])
}
for n,(title,names) in icon_groups.items():
    t=task(n,title,(3,),note='Um master branco tintável por símbolo, sem fundo/badge/sombra/texto. SVG editável é adequado à geometria simples; PNG final separado. Não criar cópia do mesmo ícone por tela/cor.')
    for name in names: asset(t,'ICON_'+name.upper(),f'shared/icons/icon_{name}.png',(256,256),(40,40),'icone',f'Ícone {name}, leitura em 32–48 px, traço editorial coerente de 2–3 px na exibição, margem uniforme. Desenho claro tintável; fundo alfa real.')

t=task(9,'Indicadores do HUD e componentes da DRE',(3,5))
for name,folder,sz,ds,b in [
 ('hud_pill','hud/indicators',(256,128),(280,60),24),('kpi_tile','hud/indicators',(256,128),(400,112),24),('progress_track','hud/progress',(128,64),(480,16),6),('progress_fill','hud/progress',(128,64),(480,16),6),('nav_underline','hud/progress',(128,16),(240,4),1),
 ('pill_normal','shared/components/badges',(256,128),(180,52),24),('pill_selected','shared/components/badges',(256,128),(180,52),24),('pill_disabled','shared/components/badges',(256,128),(180,52),24),
 ('toast_neutral','shared/components/feedback',(256,128),(680,88),24),('toast_success','shared/components/feedback',(256,128),(680,88),24),('toast_warning','shared/components/feedback',(256,128),(680,88),24),
 ('table_header','dre/components',(256,128),(1240,52),16),('table_row_neutral','dre/components',(256,128),(1240,48),16),('table_row_subtotal','dre/components',(256,128),(1240,48),16),('table_row_positive','dre/components',(256,128),(1240,48),16),('table_row_negative','dre/components',(256,128),(1240,48),16),('table_total_column','dre/components',(128,256),(180,600),16),('chart_panel','dre/components',(256,256),(560,300),24)]:
    component(t,name,folder,sz,ds,b)

t=task(10,'Geografia e contrato de coordenadas do mapa',note='Definir no map-layout.json posições/polígonos das cinco zonas e da implantação central. Base sem prédios interativos, nomes, pinos, brilhos ou HUD. Composição aprovada difere da cena atual; registrar remapeamento necessário, sem modificar a cena.')
a=asset(t,'MAP_BASE','location/map/map_base.png',(1920,1080),(1280,848),'cenario','Geografia urbana isométrica editorial: terreno, ruas, rio, pontes e vegetação baixa, com espaços livres para universidade, banco, comércio, residencial, corporativo e restaurante central. Mesmo enquadramento de todas as camadas.','rgba_opaco')
a['display_fit']='cover/crop somente na montagem: todas as camadas recebem a mesma transformação'
a['coordinate_space']='map1920x1080'; a['pivot']=[0,1]

t=task(11,'Construções e bairros do mapa',(10,),note='Cinco PNGs full-frame com a origem do master. Pixels fora da construção/bairro transparentes. Não recompor uma nova cidade a cada chamada.')
for loc in CAT['Locations']:
    a=asset(t,'MAP_'+loc['id'].upper(),f"location/landmarks/map_{loc['id']}.png",(1920,1080),(1280,848),'camada_mapa',f"Landmark isolado: {loc['name']}. Câmera, luz e implantação definidos no ART-10; sem texto ou ícone. Full-frame alinhado ao mapa.",catalog_id=loc['id'],source=loc['source'])
    a['coordinate_space']='map1920x1080';a['pivot']=[0,1]

t=task(12,'Restaurantes centrais e camada frontal do mapa',(10,11))
for rest in CAT['Restaurants']:
    a=asset(t,'MAP_'+rest['id'],f"location/restaurants/map_{rest['id'].lower()}.png",(1920,1080),(1280,848),'camada_mapa',f"Restaurante central {rest['name']}, mesmo lote, porta/escala/pivot entre três variantes; toldo roxo sem nome. Full-frame transparente fora da construção.",catalog_id=rest['id'],source=rest['source'])
    a['coordinate_space']='map1920x1080';a['pivot']=[0,1]
a=asset(t,'MAP_FOREGROUND','location/map/map_foreground.png',(1920,1080),(1280,848),'camada_mapa','Vegetação e pequenos props de primeiro plano que ocluem construções. Sem pessoas reconhecíveis; camada alfa alinhada à origem do mapa.')
a['coordinate_space']='map1920x1080';a['pivot']=[0,1]

t=task(13,'Marcadores, realces e rótulos vazios do mapa',(10,11,6))
for loc in CAT['Locations']:
    a=asset(t,'OVERLAY_'+loc['id'].upper(),f"location/overlays/selection_{loc['id']}.png",(1920,1080),(1280,848),'overlay',f"Máscara/realce suave da região {loc['name']} derivada do polígono do map-layout.json. Nenhum prédio, pino, texto ou bloom. Alinhada ao master.",catalog_id=loc['id'],source=loc['source'])
    a['coordinate_space']='map1920x1080';a['pivot']=[0,1]
for state in ['normal','selected','disabled']:
    component(t,f'pin_{state}','location/markers',(256,256),(80,80),0)
    component(t,f'map_label_{state}','location/markers',(256,128),(260,76),24)
component(t,'map_label_tip','location/markers',(64,64),(20,20),0)
a=asset(t,'OVERLAY_CENTER','location/overlays/selection_center.png',(1920,1080),(1280,848),'overlay','Realce central discreto do restaurante, sem construção embutida. Mesma origem do mapa; não usar halo neon.')
a['coordinate_space']='map1920x1080';a['pivot']=[0,1]

t=task(14,'Arte da identificação',(1,3,4,5),note='Somente cenário, ornamentos e pessoa separados; título, Nome, RA, erro e Cadastrar são TMP. Identificação não deve mostrar caixa/score/mês antes da sessão.')
asset(t,'ID_ENVIRONMENT','identification/backgrounds/street_restaurant.png',(1920,1080),(1920,1080),'cenario','Fachada/bairro acolhedor brasileiro contemporâneo com área livre à direita para formulário. Sem personagem, placa escrita ou UI.','rgba_opaco',refs_used=('02-abertura.png',))
for side in ['left','right']: asset(t,'ID_ORNAMENT_'+side.upper(),f'identification/ornaments/arc_{side}.png',(512,1024),(320,860),'ornamento',f'Arcos e círculos discretos da identificação roxa, ornamento {side} isolado sem fundo.')
asset(t,'CH01_WELCOME','characters/CH01/ch01_welcome.png',(1024,1536),(420,760),'personagem','CH01 em pé com prancheta, expressão de boas-vindas natural. Mesma pessoa do master; sem cenário ou formulário.',characters_used=('CH01',),refs_used=('02-abertura.png','03-gestao.png'))

t=task(15,'Fachadas dos três restaurantes',(12,),note='Fachada isolada por tipo, não card rasterizado; pode ser reaproveitada no estabelecimento. Referência estilística comum, arquitetura compatível com variante do mapa.')
for rest in CAT['Restaurants']:
    asset(t,'FACADE_'+rest['id'],f"restaurant/facades/{rest['id'].lower()}.png",(1024,768),(480,360),'fachada',f"Fachada isolada {rest['name']}, vista frontal em três quartos, detalhes próprios do posicionamento; toldo/detalhe roxo, sem palavras, pessoas, pratos ou moldura.",catalog_id=rest['id'],source=rest['source'],refs_used=('02-abertura.png','03-gestao.png'))

t=task(16,'Cinco vinhetas de localização',(11,),note='Vinhetas sem rótulos. Miolo do cenário opaco com exterior/recorte alfa; pessoas e UI não incorporadas.')
for loc in CAT['Locations']:
    asset(t,'VIGNETTE_'+loc['id'].upper(),f"location/illustrations/vignette_{loc['id']}.png",(960,540),(520,292),'vinheta',f"Vinheta ambiental {loc['name']}, coerente com a construção do mapa. Sem texto, ícones ou pessoas canônicas.",catalog_id=loc['id'],source=loc['source'])

for n,prefix,title in [(17,'LAN','Produtos de lanches'),(18,'JAP','Produtos japoneses'),(19,'FRA','Produtos franceses')]:
    t=task(n,title,(15,),note='Três sprites independentes. Consultar o sprite atual indicado no catálogo como conteúdo e os concepts como estilo. Nomes não especificam receita completa: registrar a composição visual proposta, sem mudar dados do jogo.')
    for prod in CAT['Products']:
        if prod['id'].startswith(prefix+'_'):
            asset(t,'PRODUCT_'+prod['id'],f"menu_pricing/products/{prefix.lower()}/{prod['id'].lower()}.png",(512,512),(250,250),'produto',f"Produto {prod['name']}; desenho adulto apetitoso, prato/bandeja em três quartos, sem nome/preço ou sombra larga. Usar o sprite existente como referência de composição e distinguir os níveis simples/médio/premium.",catalog_id=prod['id'],source=prod['source'],refs_used=('02-abertura.png',))

eq_groups={20:('Equipamentos básicos de cozinha',['bancada_inox_preparo','conjunto_panelas_industriais','fogao_industrial_6_bocas','pia_industrial_inox'],'basic'),21:('Equipamentos básicos de estrutura',['freezer_horizontal','geladeira_comercial_dupla','mobiliario_basico','sistema_pdv_computador'],'basic'),22:('Equipamentos de lanches',['chapa_profissional_podrao','fritadeira_eletrica_dupla_podrao'],'lanches'),23:('Equipamentos japoneses',['balcao_refrigerado_sushi','facas_especiais_japones','panela_eletrica_arroz'],'japones'),24:('Equipamentos franceses',['adega_climatizada','forno_combinado_inteligente','mobiliario_luxo'],'frances')}
for n,(title,ids,folder) in eq_groups.items():
    t=task(n,title,(3,),note='Um PNG por equipamento. Não embutir adquirido/bloqueado/premium, preço, moldura ou carrinho; são overlays de UI. Preservar IDs do catálogo, inclusive nomes herdados podrao.')
    for eid in ids:
        eq=next(x for x in CAT['Equipments'] if x['id']==eid)
        brief=f"{eq['name']}. Equipamento comercial em três quartos com frente e lado direito visíveis, luz no alto à esquerda, contorno ameixa, metal fosco cinza-ardósia em 2–3 tonalidades, silhueta reconhecível. Mesmo enquadramento e escala relativa da família."
        if 'mobiliario' in eid: brief+=' Representar conjunto legível com uma mesa e algumas cadeiras; quantidade contratada permanece em texto, não desenhar 40 miniaturas.'
        if eid=='sistema_pdv_computador': brief+=' Conjunto de terminal/computador, monitor e impressora de recibo; acessórios coerentes com o sprite atual.'
        asset(t,'EQUIPMENT_'+eid.upper(),f'equipment/{folder}/{eid}.png',(512,512),(250,250),'equipamento',brief,catalog_id=eid,source=eq['source'],refs_used=('03-gestao.png',))

t=task(25,'Financeiro e quatro opções de crédito',(1,4,6,7),note='Quatro opções reais, não as três do concept. Cenário, pessoa e objetos separados; taxas/parcelas/limites em TMP.')
asset(t,'BANK_ENVIRONMENT','financial/backgrounds/bank_office.png',(1024,1536),(480,840),'cenario','Ambiente de banco/escritório em quadro vertical, profissional e sóbrio. Sem pessoa, marca, placa escrita ou cards.','rgba_opaco',refs_used=('03-gestao.png',))
asset(t,'CH01_BANK','characters/CH01/ch01_bank_options.png',(1024,1536),(420,760),'personagem','CH01 em pose orientando opções com mão aberta e prancheta; manter rosto, cabelo, argolas e figurino do master. Sem cenário/cards.',characters_used=('CH01',),refs_used=('03-gestao.png',))
credit_briefs={'sem_credito':'Documento simples com check separado pela UI, indicando escolha sem crédito; objeto sem texto.','microcredito_empresarial':'Pequeno cofre/caixa de capital comercial, objeto editorial simples sem valores.','credito_empresarial_padrao':'Conjunto de capital de giro com papéis comerciais e poucas cédulas sem inscrições.','credito_de_investimento':'Maleta profissional e contrato sem texto, indicando investimento.'}
for credit in CAT['CreditLines']:
    asset(t,'CREDIT_'+credit['id'].upper(),f"financial/credit/{credit['id']}.png",(512,512),(120,120),'credito',credit_briefs[credit['id']],catalog_id=credit['id'],source=credit['source'],refs_used=('03-gestao.png',))

t=task(26,'Retratos de equipe e ambientes de trabalho',(1,2),note='Cinco funções do catálogo. Retrato e fundo são arquivos distintos. Usar master canônico, não criar um novo rosto para cada função ou tela.')
role_char={'attendant':'CH01','grill_cook':'CH02','manager':'CH03','head_chef':'CH05','sushi_chef':'CH06'}
for role in CAT['Roles']:
    cid=role_char[role['id']]
    asset(t,'PORTRAIT_'+role['id'].upper(),f"team/portraits/{role['id']}.png",(512,512),(240,240),'personagem',f"Retrato de meio corpo da identidade {cid} para função {role['id']}. Sem fundo, nome, salário ou ferramentas que cortem mãos; aparência igual ao master.",catalog_id=role['id'],source=role['source'],characters_used=(cid,),refs_used=('03-gestao.png',))
for env in ['service_counter','grill_kitchen','management_office','french_kitchen','sushi_counter']:
    asset(t,'TEAM_ENV_'+env.upper(),f'team/backgrounds/{env}.png',(960,720),(320,240),'cenario',f'Ambiente de trabalho {env}, sem pessoas ou texto. Mesma arquitetura, materiais e perspectiva editorial do restaurante.','rgba_opaco',refs_used=('03-gestao.png',))

task(27,'Prova de montagem do estabelecimento e revisão',(3,6,7,9,15,26),scope='validacao',note='Não cria sprites duplicados. Produzir preview 1920x1080 em art-source/previews reutilizando fachada, KPIs, checklist, ícones e personagens. Não alterar cena/prefab. Registrar quais arquivos cada tela consome.')

t=task(28,'Ilustração em camadas da DRE mensal',(1,9),note='Tabela, números, títulos, eixos e barras de gráfico são UI/dados. Mesa, personagem e objetos independentes; definir pontos de encaixe no dre-layout.json para evitar membros/props desalinhados.')
asset(t,'DRE_ENVIRONMENT','dre/monthly/office_background.png',(1024,768),(560,420),'cenario','Escritório do restaurante visto de frente, janela com detalhe de toldo roxo, sem pessoa, mesa frontal, textos ou gráficos.','rgba_opaco',refs_used=('04-dre-mensal.png',))
asset(t,'DRE_DESK','dre/props/desk_foreground.png',(1024,512),(560,280),'prop','Mesa de análise em primeiro plano, recorte transparente; sem pessoa nem props incorporados. Encaixe definido em dre-layout.json.',refs_used=('04-dre-mensal.png',))
asset(t,'CH01_DRE','characters/CH01/ch01_analyzing.png',(768,1024),(360,480),'personagem','CH01 sentada analisando relatório, sem mesa/fundo e sem papel incorporado. Mesma face e figurino; mãos compatíveis com prop report_blank. Definir encaixe com mesa.',characters_used=('CH01',),refs_used=('04-dre-mensal.png',))
for name in ['calculator','coffee_cup','report_blank']:
    asset(t,'DRE_PROP_'+name.upper(),f'dre/props/{name}.png',(256,256),(96,96),'prop',f'Prop {name} da cena de DRE, transparente, sem slogans, números ou dados. Mesma perspectiva e luz.',refs_used=('04-dre-mensal.png',))

t=task(29,'Personagens da DRE trimestral',(1,28),note='Reaproveitar cenário/mesa/props mensais. Duas pessoas em dois PNGs, nenhuma conversa em imagem única. Tabela comparativa e gráficos por UI.')
for cid,pose in [('CH01','discussing'),('CH04','discussing')]:
    asset(t,cid+'_DISCUSSING',f'characters/{cid}/{cid.lower()}_{pose}.png',(768,1024),(360,480),'personagem',f'{cid} sentado em conversa profissional sobre relatório, identidade e roupa iguais ao master; sem outra pessoa, fundo, mesa, papel, caneca, props ou dados incorporados. Usar os props independentes do ART-28. Definir posições relativas no dre-layout.json.',characters_used=(cid,),refs_used=('05-dre-trimestral.png',))

event_groups={30:('Eventos de fornecedores e estoque',['bulk_purchase_opportunity','supplier_discount','supplier_delay','ingredient_price_increase','excessive_food_waste']),31:('Eventos de demanda e concorrência',['extra_local_demand','lunch_demand_peak','corporate_order','competitor_temporary_closure','new_competitor']),32:('Eventos da operação e equipe',['freezer_breakdown','health_inspection','insufficient_team','team_productivity_boost','payment_system_failure']),33:('Eventos de comunicação e contexto urbano',['influencer_mention','positive_online_review','negative_review','local_festival','heavy_rain'])}
for n,(title,ids) in event_groups.items():
    t=task(n,title,(1,2,15,20,21,22,23,24),note='Cinco vinhetas independentes com alfa. Preferir situação/objetos isolados; não introduzir pessoas canônicas sem referenciar o master. Opções, efeitos e título em UI. O catálogo atual tem três opções; não fixar duas no raster.')
    for eid in ids:
        ev=next(x for x in CAT['Events'] if x['id']==eid)
        brief=ev['data'].get('description','').replace(' TESTE DE ALTERACAO','')
        asset(t,'EVENT_'+eid.upper(),f'events/illustrations/{eid}.png',(768,512),(640,426),'evento',f'Situação editorial {eid}: {brief} Representar com cenário/props legíveis e sem UI/texto. Personagens recorrentes, se necessários, devem ser layers separados existentes; não assar seus rostos nesta vinheta.',catalog_id=eid,source=ev['source'],refs_used=('01-hub.png','03-gestao.png'))

t=task(34,'Conclusão, dificuldade e estados vazios',(1,15,28),note='Concluir sem confete/troféu infantil; dificuldade sem dramatização. Reutilizar componentes comuns dos modais de reset, salvar/sair, saldo insuficiente, carrinho e fechamento.')
for aid,path,brief in [('REPORT_CONCLUSION','reports/illustrations/conclusion.png','Objeto editorial de conclusão: pasta/relatório e estabelecimento em crescimento, sem pessoas, texto, medalha ou números.'),('BANKRUPTCY_OVERLAY','reports/illustrations/business_difficulty.png','Vinheta isolada de estabelecimento em dificuldade: movimento reduzido e operação parada, sem palavras, pessoas canônicas ou humilhação. Compatível com fachada existente.'),('EMPTY_CART','system/empty_states/cart_empty.png','Carrinho comercial vazio como pequeno objeto editorial, sem ícone de alerta ou label.'),('EMPTY_TEAM','system/empty_states/team_pending.png','Crachás/proposta de contratação sobre prancheta, sem rostos, nomes ou texto.'),('EMPTY_RESULTS','system/empty_states/results_pending.png','Pasta e relatório vazio, sem dados/gráfico de exemplo ou palavras.')]:
    asset(t,aid,path,(768,512),(480,320),'ilustracao',brief,refs_used=('03-gestao.png','05-dre-trimestral.png'))
asset(t,'CH01_CONCERNED','characters/CH01/ch01_concerned.png',(1024,1536),(420,760),'personagem','CH01 em expressão serena de preocupação diante de dificuldade. Preservar identidade, roupa, proporção e dignidade; sem cenário/outros personagens.',characters_used=('CH01',),refs_used=('03-gestao.png',))

t=task(35,'Loading e pistas de tutorial',(4,5,8),note='Spinner gira por UI; não gerar uma sprite sheet. Tutorial utiliza CH01 e componentes comuns. Ícone e texto continuam separados.')
asset(t,'UI_SPINNER','system/loading/spinner.png',(128,128),(48,48),'componente','Anel simples com abertura e alfa real, tintável, rotação controlada pela UI; sem porcentagem ou texto.')
for name in ['tap','drag','focus_ring']:
    asset(t,'TUTORIAL_'+name.upper(),f'tutorials/gestures/{name}.png',(256,256),(64,64),'icone',f'Pista visual de tutorial {name}: desenho simples legível, alfa real, sem palavras ou mão caricata gigante. Retirar da tela quando tutorial não estiver ativo.')

task(36,'Validação final e mapa de integração',tuple(range(1,36)),scope='validacao',note='Sem gerar arte nova ou aplicar às cenas. Conferir arquivos, alfa, dimensões, bounds, estados sem saltos, identidade canônica, alinhamento do mapa, leitura a 100% e escala móvel. Produzir previews separados 1920x1080 e atualizar integração/status reais; não confundir referências aprovadas com sprites finais.')

t=task(37,'Complementos de BMC e relatório documental',(6,7,9),scope='documental',note='Previsto em arq.pdf, ainda sem tela/modelo final implementados. Criar somente os símbolos que não existem no kit. Nove blocos do BMC, balanço e fluxo de caixa são layout/texto/dados, não nove screenshots rasterizados. Rótulos e período ficam dinâmicos, aceitando 3 ou 12 meses.')
for name in ['partners','value_proposition','customer_relationship','sales_channels']:
    asset(t,'ICON_'+name.upper(),f'reports/bmc/icons/{name}.png',(256,256),(40,40),'icone',f'Símbolo sóbrio do bloco BMC {name}; alfa real, master tintável, sem label. Outros cinco blocos reutilizam ícones do kit.')

t=task(38,'Apoio visual para debriefing e justificativas',(1,4,5,28),scope='documental',note='Previsto no relatório técnico; não há UI atual. Produzir apenas pose de apoio; formulários, respostas, feedback e status offline reutilizam componentes. Nenhum serviço/API, backend ou painel docente será criado neste lote.')
asset(t,'CH01_GUIDING','characters/CH01/ch01_guiding_question.png',(1024,1536),(420,760),'personagem','CH01 orientando reflexão com gesto natural de pergunta; sem balão/texto, face/roupa iguais ao master. Postura profissional, sem dar respostas prontas incorporadas à imagem.',characters_used=('CH01',),refs_used=('03-gestao.png',))

t=task(39,'Função garçom se entrar no catálogo',(1,2,26),scope='condicional',note='GARCOM existe no enum, mas não há RoleData correspondente. Só produzir este lote quando solicitado para essa extensão. Não adicionar role nem alterar balanceamento automaticamente.')
asset(t,'CH07_CANONICAL','characters/CH07/ch07_canonical.png',(1024,1536),(420,760),'personagem','Nova identidade adulta para função garçom; distinta das seis identidades existentes, roupa profissional compatível com paleta. Definir e registrar aparência no lote.',characters_used=('CH07',),refs_used=('03-gestao.png',))
asset(t,'PORTRAIT_WAITER','team/portraits/waiter.png',(512,512),(240,240),'personagem','Retrato da identidade CH07, sem fundo/texto/salário; dependente do master produzido neste lote.',characters_used=('CH07',),refs_used=('03-gestao.png',))
task(40,'Materiais de loja e painel docente sob demanda',(36,),scope='opcional',note='Fora do pacote obrigatório do jogo atual. Ícone de aplicativo, capturas de loja e painel docente exigem briefing/tamanhos próprios quando solicitados. Não inventar logotipo Cogna, redes sociais, layouts de marketing ou novos assets sem esse pedido.')

tokens={'canvas_px':[1920,1080],'topbar_px':[1920,88],'navbar_px':[1920,96],'content_height_px':896,'content_padding_px':24,'gutter_px':24,
 'display_dimension_semantics':'display_size_px é a caixa de layout. Personagens/objetos usam contain/preserveAspect; nunca escalar x e y independentemente. Cenários podem usar cover com crop registrado. Camadas do mapa compartilham a mesma transformação uniforme, escala e offset.',
 'map_display_px':[1280,848],'side_panel_display_px':[568,848],'map_origin':[0,0],'map_layers_px':[1920,1080],
 'palette':{'violet':'#6D28D9','plum':'#24113D','lavender':'#B99AE8','ivory':'#F5F0E8','ink':'#30243C','teal':'#26776A','coral':'#C55457','amber':'#E7B65A'},
 'font':'Texto nativo TextMeshPro; manter fonte do projeto ou família sans aprovada. Nenhuma palavra rasterizada nos sprites.',
 'sprite_import_proposal':{'textureType':'Sprite (2D and UI)','spriteMode':'Single','pixelsPerUnit':100,'filterMode':'Bilinear','generateMipmaps':False,'alphaIsTransparency':True,'sRGBTexture':True,'compression':'Sem compressão na QA; ajustar atlas/plataforma após teste','meshType':'FullRect para UI; avaliar Tight apenas em ilustrações recortadas','maxTextureSize':'>= dimensão máxima real do export','nine_slice':'Importar bordas l,b,r,t do manifesto; tipo Sliced apenas em componentes. Não aplicar nos objetos/rostos.'},
 'current_project_caveats':['Identificação ainda usa referência 1280x720; GameScene principal usa 1920x1080; há CanvasScalers 800x600 em painéis. Uniformização fica na integração posterior.','Canvas de produção solicitado é 1920x1080. Valores de display são caixas de layout propostas, não medições finais de todos os RectTransforms.','Não alterar cenas, prefabs, scripts, dados econômicos ou IDs para produzir arte.']}

aux={10:[{'path':'docs/art/layouts/map-layout.json','schema':'docs/art/schemas/map-layout.schema.json','required_fields':['master_size_px','coordinate_origin','camera','display_transform','zones','layers','hit_areas','central_site_bounds_px']}],28:[{'path':'docs/art/layouts/dre-layout.json','schema':'docs/art/schemas/dre-layout.schema.json','required_fields':['canvas_px','coordinate_origin','camera','layers','anchors','occlusion_masks']}],29:[{'path':'docs/art/layouts/dre-layout.json','schema':'docs/art/schemas/dre-layout.schema.json','required_fields':['canvas_px','coordinate_origin','camera','layers','anchors','occlusion_masks']} ]}
for t in tasks:t['auxiliary_deliverables']=aux.get(int(t['id'][-2:]),[])
schemas=PROD/'schemas';schemas.mkdir(exist_ok=True);(PROD/'layouts').mkdir(exist_ok=True)
vec={'type':'array','items':{'type':'number'},'minItems':2,'maxItems':2}
layer={'type':'object','required':['asset_id','position_px','scale','order','pivot'],'properties':{'asset_id':{'type':'string'},'position_px':vec,'scale':{'type':'number','exclusiveMinimum':0},'order':{'type':'integer'},'pivot':vec}}
map_schema={'$schema':'https://json-schema.org/draft/2020-12/schema','title':'Map composition contract','type':'object','required':aux[10][0]['required_fields'],'properties':{'master_size_px':{'const':[1920,1080]},'coordinate_origin':{'const':'top_left_y_down'},'camera':{'type':'object','required':['projection','orientation_description','light_direction_description']},'display_transform':{'type':'object','required':['uniform_scale','offset_px','target_viewport_px','fit_mode'],'properties':{'uniform_scale':{'type':'number','exclusiveMinimum':0},'offset_px':vec,'target_viewport_px':{'type':'array','minItems':4,'maxItems':4},'fit_mode':{'enum':['contain','cover']}}},'zones':{'type':'array','minItems':5,'maxItems':5,'items':{'type':'object','required':['catalog_id','polygon_px','label_anchor_px']}},'layers':{'type':'array','items':layer},'hit_areas':{'type':'array','minItems':5,'items':{'type':'object','required':['catalog_id','polygon_px']}},'central_site_bounds_px':{'type':'array','minItems':4,'maxItems':4}}}
dre_schema={'$schema':'https://json-schema.org/draft/2020-12/schema','title':'DRE composition contract','type':'object','required':aux[28][0]['required_fields'],'properties':{'canvas_px':{'const':[1920,1080]},'coordinate_origin':{'const':'top_left_y_down'},'camera':{'type':'object','required':['orientation_description','light_direction_description']},'layers':{'type':'array','items':layer},'anchors':{'type':'object','required':['ch01_hand','paper_grip','desk_front_edge'],'additionalProperties':vec},'occlusion_masks':{'type':'array','description':'Empty if separate z-order is sufficient. Otherwise each mask records asset_id, polygon_px and which layers it clips.'}}}
for name,obj in [('map-layout.schema.json',map_schema),('dre-layout.schema.json',dre_schema)]: (schemas/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')

source_review={
 'date':'2026-10-14',
 'documents':[
 {'file':r'C:\Users\Pichau\Downloads\arq.pdf','pages':25,'use':'EquipmentData/RoleData/CreditLineData são modelos e exemplos, não inventário completo. DRE/BMC/fluxo de caixa/balanço e ciclo de 12 rodadas são escopo documental. Páginas relevantes 5–8, 10–12, 22–24.'},
 {'file':r'C:\Users\Pichau\Downloads\Relatorio_Reservado_Jogo_Unity_Cogna.docx','use':'Contexto POC, seis decisões e camada de debriefing/justificativas/diagnóstico. Não contém lista exaustiva de equipamentos. Não transportar orçamento/recomendações administrativas para prompts de arte.'},
 {'file':r'C:\Users\Pichau\Downloads\Opportunity Explorer.pdf','pages':13,'use':'Documento de métricas Amazon Seller/Vendor. Sem conteúdo de assets do jogo; excluído do inventário.'}],
 'priority':'Pedido atual e catálogo Unity definem conteúdo confirmado; concepts aprovados definem linguagem visual; documentos definem complementos previstos sem substituir código atual.',
 'conflicts':['Documentos: 12 rodadas e DRE anual; implementação atual: ciclo trimestral de 3 meses. Não rasterizar contagem nem mudar motor.','Documento EventData prevê 2 opções; 20 assets atuais têm 3. Cards reutilizáveis com quantidade dinâmica.','Concept financeiro mostra 3 opções; catálogo possui 4 IDs únicos.','Concept equipe mostra 3 funções; catálogo contém 5 e enum inclui garçom sem RoleData.','freezer_breakdown contém texto de teste no título/descrição; manter ID e não levar texto de teste para arte.','Produtos usam nomes genéricos sem receita; consultar sprites atuais e registrar composição visual, não mudar regras de produto.']}

manifest={'schema_version':1,'project':'Jogos de Empresa Cogna','date':'2026-10-14','asset_root':str(ROOT),'phase':'planejamento; nenhuma nova arte de produção gerada','reference_copies':list(refs),
 'canvas_px':[1920,1080],'catalog_counts':{k:len(v) for k,v in CAT.items()},'characters':characters,'assets':assets,'tasks':tasks,'rules':[
 'Um objeto/ícone/pessoa/componente por arquivo; não entregar telas prontas achatadas como sprites.',
 'PNG RGBA com alfa real em recortes; cenários/barras de preenchimento podem ter miolo opaco. Sem checkerboard incorporado.',
 'Compartilhar ícones/componentes: não duplicar por tela, número de instâncias ou cor. Estados de botão têm mesmos bounds/pivot.',
 'Nenhum texto, preço, quantidade, logo oficial inventado, gráfico ou valor ilustrativo incorporado ao bitmap.',
 'IDs de dados preservados e registrados. Não alterar cenas/catálogos no trabalho de arte.',
 'Ilustrações e alterações visuais usam image_gen integrado. Componentes geométricos/ícones simples podem usar SVG editável e export PNG exato. Seguir skill imagegen; não trocar para API/CLI sem pedido.',
 'Salvar arte final em Assets/Art/new. Registrar prompt, origem, referência canônica, dimensões reais, SHA256 e QA por asset.',
 'Conferir os assets sobre fundos claros e escuros, em tamanho de exibição e no canvas 1920x1080.',
 'Executar só a tarefa solicitada e suas peças listadas. Pré-requisitos indicam ordem de produção; não gerar lotes adicionais automaticamente.'
 ],'counts':{'assets_total':len(assets),'assets_nucleo':sum(a['scope']=='nucleo' for a in assets),'assets_documental':sum(a['scope']=='documental' for a in assets),'assets_condicional':sum(a['scope']=='condicional' for a in assets),'tasks_total':len(tasks)}, 'dynamic_ui_not_assets':['Nomes, RA, labels e tooltips','Preço, salário, parcela, juros, meses e contagens','Valores DRE, BMC preenchido, balanço, fluxo de caixa','Barras, pontos, eixos e legendas de gráficos','Quantidade de opções de evento, cards instanciados e indicadores de status','Professores/alunos, ranking ou resultados acadêmicos']}
for data,name in [(manifest,'manifest.json'),(tasks,'tasks.json'),(tokens,'tokens.json'),(characters,'characters.json'),(source_review,'sources.json'),(CAT,'catalogos.json')]:
    (PROD/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')

task_dir=PROD/'tasks'; task_dir.mkdir(exist_ok=True)
for t in tasks:
    selected=[a for a in assets if a['task_id']==t['id']]
    command=f"Use $cogna-artista para executar somente {t['id']} — {t['title']}. Leia D:/Cogna/jogos_empresa/docs/art/tasks/{t['id']}.txt e o manifesto. Produza apenas os arquivos deste lote em Assets/Art/new, separados, no estilo aprovado, com alfa e tamanhos especificados. Preserve as identidades dos personagens e registre prompts/QA. Não implemente UI nem execute os próximos lotes."
    t['request_prompt']=command
    lines=[t['id']+' — '+t['title'],'Escopo: '+t['scope'],'Pré-requisitos: '+(', '.join(t['dependencies']) or 'nenhum'),'',t['note'],'','COMANDO PARA O CODEX',command,'','LEITURAS','docs/art/manifest.json, tokens.json, characters.json e sources.json','Referências em art-source/references/concepts; masters canônicos em Assets/Art/new/characters/CHxx após ART-01/02.','','ENTREGÁVEIS']
    for a in selected:
        lines.extend([f"{a['id']} | {a['path']}",f"Export {a['export_size_px'][0]}x{a['export_size_px'][1]} px | display {a['display_size_px'][0]}x{a['display_size_px'][1]} px | alfa {a['alpha_policy']} | 9-slice l,b,r,t {a['nine_slice_lbrt_px']} | pivot {a['pivot']}",a['brief'],'Fonte: '+str(a['source'] or 'concept aprovado / especificação de produção'),'Personagens: '+(', '.join(a['character_ids']) or 'nenhum'), ''])
    if not selected: lines.append('Nenhum sprite novo; seguir a nota do lote para validação ou extensão opcional.')
    if t['auxiliary_deliverables']:
        lines.extend(['','CONTRATO DE MONTAGEM OBRIGATÓRIO'])
        for sidecar in t['auxiliary_deliverables']:
            lines.extend([sidecar['path'],'Schema: '+sidecar['schema'],'Campos: '+', '.join(sidecar['required_fields']),'Câmera/orientação/luz iguais à referência. Posições em pixels com origem no alto à esquerda; escala uniforme, ordem de camadas e pivot explícitos.','Mapa: cinco IDs de zona com polígonos de seleção/hit-area, labels e implantação central. DRE: pontos de encaixe de mão, mesa e papel; máscaras de oclusão quando necessárias. ART-29 atualiza posições da conversa mantendo os mesmos props e registrando âncoras do mentor.'])
    lines.extend(['CRITÉRIOS DE ENTREGA','Cada PNG separado e no caminho do manifesto; nada permanece apenas em pasta de geração.','Alfa real nos recortes e dimensões exatas verificadas; não fingir export correto quando divergir.','Nomes e IDs preservados; sem texto, UI ou gráfico incorporado às ilustrações.','Validar tecnicamente com o helper do agente e inspecionar visualmente estilo/identidade.','Atualizar status somente após arquivos reais e QA. Registrar prompts em docs/art/prompts/'+t['id']+'.json e preview de QA em art-source/previews.','Não mudar o jogo; não produzir outros lotes automaticamente.'])
    lines.append('display_size_px é caixa de layout: preserveAspect/contain nos recortes, sem esticar. Componentes 9-slice usam bordas preservadas. Fontes SVG/código editáveis precisam existir no source_editable especificado quando production_method=vector_or_code.')
    (task_dir/(t['id']+'.txt')).write_text('\n'.join(lines),encoding='utf-8')
# Serialize prompts added above.
(PROD/'tasks.json').write_text(json.dumps(tasks,ensure_ascii=False,indent=2),encoding='utf-8')
(PROD/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
for d in ['prompts','previews','qa']:(PROD/d).mkdir(exist_ok=True)

e=html.escape
task_rows=''.join(f"<tr><td><a href='tasks/{t['id']}.txt'>{t['id']}</a></td><td>{e(t['title'])}</td><td>{len(t['asset_ids'])}</td><td>{e(t['scope'])}</td><td>{e(', '.join(t['dependencies']) or '—')}</td></tr>" for t in tasks)
asset_rows=''.join(f"<tr data-search='{e((a['id']+' '+a['path']+' '+a['brief']).lower(),quote=True)}'><td>{e(a['id'])}<small>{e(a['path'])}</small></td><td><a href='tasks/{a['task_id']}.txt'>{a['task_id']}</a></td><td>{a['export_size_px'][0]}×{a['export_size_px'][1]}</td><td>{a['display_size_px'][0]}×{a['display_size_px'][1]}</td><td>{e(a['alpha_policy'])}<small>9-slice {a['nine_slice_lbrt_px']}</small></td><td>{e(a['brief'])}</td></tr>" for a in assets)
eq_rows=''.join(f"<tr><td>{e(x['id'])}</td><td>{e(x['name'])}</td><td>{next(a['task_id'] for a in assets if a['kind']=='equipamento' and a['catalog_id']==x['id'])}</td></tr>" for x in CAT['Equipments'])
character_cards=''.join(f"<article><h3>{c['id']} {e(c['name'])}</h3><p>{e(c['appearance'])}</p><small>Master futuro: {e(c['canonical_asset'])}</small></article>" for c in characters)
task_details=''.join(f"<details><summary>{t['id']} · {e(t['title'])} · {len(t['asset_ids'])} arquivos</summary><p>{e(t['note'])}</p><p>Pré-requisitos: {e(', '.join(t['dependencies']) or 'nenhum')} · Escopo: {e(t['scope'])}</p><pre>{e(t['request_prompt'])}</pre><a href='tasks/{t['id']}.txt'>Abrir especificação completa</a></details>" for t in tasks)
total=manifest['counts']
page=f'''<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Plano de produção de arte Cogna</title><style>
:root{{--plum:#24113D;--purple:#6D28D9;--ivory:#F5F0E8;--ink:#30243C}}*{{box-sizing:border-box}}body{{margin:0;font-family:Segoe UI,Arial,sans-serif;background:var(--ivory);color:var(--ink);line-height:1.55}}header{{background:var(--plum);color:#fff;padding:48px max(24px,calc((100vw - 1200px)/2))}}h1{{font-size:clamp(32px,5vw,54px);line-height:1.1}}h2{{font-size:30px;line-height:1.2;margin-top:0}}h3{{font-size:20px}}header p{{max-width:950px;color:#e5dcef}}nav{{display:flex;gap:14px;flex-wrap:wrap}}nav a{{color:#fff}}main{{max-width:1280px;margin:auto;padding:30px}}section{{margin-bottom:42px}}a{{color:var(--purple)}}.grid{{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}}article,details{{border:1px solid #ddd3e6;border-radius:10px;background:#fff;padding:18px;margin:14px 0}}.stat{{font-size:32px;font-weight:700;color:var(--purple)}}.table{{overflow:auto;border:1px solid #ddd3e6;border-radius:8px;background:#fff}}table{{border-collapse:collapse;width:100%;min-width:850px;font-size:14px}}th{{background:var(--plum);color:#fff;text-align:left;padding:12px}}td{{border-bottom:1px solid #e4dcea;padding:12px;vertical-align:top}}small{{display:block;color:#665970;overflow-wrap:anywhere}}td:first-child{{font-weight:600;max-width:300px;overflow-wrap:anywhere}}pre{{white-space:pre-wrap;overflow-wrap:anywhere;background:#eee6f8;padding:16px;border-radius:8px;line-height:1.7}}summary{{cursor:pointer;font-weight:700}}.reference{{width:100%;height:auto;border-radius:8px}}label{{display:block;margin:16px 0}}input{{padding:12px;width:100%;border:1px solid #c6b8d5;border-radius:6px;font-size:16px}}.muted{{color:#665970}}li{{margin:8px 0}}code{{overflow-wrap:anywhere}}footer{{padding:22px;background:var(--plum);color:#fff}}@media(max-width:760px){{main{{padding:18px}}.grid{{grid-template-columns:1fr}}header{{padding:26px 20px}}}}
</style></head><body><header><small style="color:#B99AE8">JOGOS DE EMPRESA · 14/10/2026 · PLANEJAMENTO</small><h1>Produção de arte em lotes</h1><p>Assets separados para canvas 1920×1080, seguindo os concepts aprovados. Este pacote prepara o agente, o inventário e as tarefas; nenhum novo sprite de produção foi gerado nesta etapa.</p><nav><a href="#tarefas">Tarefas</a><a href="#inventario">Inventário</a><a href="#equipamentos">16 equipamentos</a><a href="#personagens">Personagens</a><a href="#contrato">Contrato técnico</a><a href="#fontes">Fontes e diferenças</a></nav></header><main>
<section><div class="grid"><article><div class="stat">{total['assets_nucleo']}</div>arquivos de produção do núcleo, contando estados separados</article><article><div class="stat">{total['tasks_total']}</div>tarefas, incluindo QA e complementos previstos</article><article><div class="stat">16 / 9 / 20</div>equipamentos / pratos / eventos confirmados</article></div><p>Além do núcleo, {total['assets_documental']} arquivos são complementos documentais e {total['assets_condicional']} dependem da entrada da função garçom no escopo. As cinco referências abaixo são cópias dos concepts já aprovados, não sprites finais nem novos desenhos.</p><details><summary>Ver referências visuais aprovadas</summary>{''.join(f'<h3>{e(name)}</h3><img class="reference" loading="lazy" src="../../art-source/references/concepts/{name}" alt="Concept aprovado {e(name)}">' for name in refs)}</details><p><a href="manifest.json">Manifesto completo</a> · <a href="tokens.json">Tamanhos e tokens</a> · <a href="characters.json">Identidades canônicas</a> · <a href="catalogos.json">Catálogos verificados</a></p></section>
<section id="tarefas"><h2>Ordem de produção e comandos prontos</h2><p>Comece por ART-01. Depois solicite um lote por vez. Os componentes compartilhados são produzidos uma vez e reaproveitados pelas interfaces. Dependências expressam a ordem para preservar o estilo e o alinhamento.</p><div class="table"><table><thead><tr><th>Lote</th><th>Entrega</th><th>Arquivos</th><th>Escopo</th><th>Depende de</th></tr></thead><tbody>{task_rows}</tbody></table></div>{task_details}</section>
<section id="contrato"><h2>Contrato técnico da arte</h2><ul>{''.join(f'<li>{e(rule)}</li>' for rule in manifest['rules'])}</ul><p><strong>Layout de referência:</strong> barra superior 1920×88, navegação inferior 1920×96, área de conteúdo 896 px de altura, margens e gutters de 24 px. Mapa 1280×848 e painel lateral 568×848. Tamanhos de arquivo não são necessariamente os tamanhos de exibição: ícones 256×256 aparecem em 32–48 px; equipamentos 512×512 aparecem em aproximadamente 250×250 px; painéis 9-slice podem crescer sem distorcer cantos.</p><p><strong>Transparência:</strong> todos os exports em PNG RGBA. Pessoas, objetos, ícones, contornos e overlays têm alfa real fora da silhueta; cenários e barras que preenchem o fundo podem ter miolo opaco. Nenhum fundo quadriculado incorporado. Sombras de UI em arquivo próprio.</p><p><strong>Mapa:</strong> todas as camadas compartilham um espaço 1920×1080 e pivot superior esquerdo. O lote ART-10 registra as posições no map-layout.json. Qualquer crop futuro exige offsets/pivot preservados. Não pedir à IA para inventar a geografia novamente em cada construção.</p><p><strong>Personagens:</strong> uma identidade canônica por pessoa, com rosto/cabelo/pele/roupa/acessórios fixos. Novas poses usam o PNG canônico como referência, não apenas descrição textual. Separar a pessoa da mesa, do cenário e de outras pessoas quando forem reutilizáveis.</p><p><strong>UI dinâmica:</strong> labels, nomes, números, mês, preço, score, gráficos e conteúdo de BMC/DRE não são imagens. Estados adquiridos/bloqueados dos equipamentos são overlays; não exigem redesenhar cada equipamento.</p><p class="muted">A integração posterior deve uniformizar CanvasScalers: identificação ainda tem 1280×720 e alguns painéis mantêm 800×600. O plano segue sua especificação 1920×1080; nenhuma configuração do jogo foi alterada.</p></section>
<section id="personagens"><h2>Biblioteca de personagens</h2><div class="grid">{character_cards}</div><p>CH01 a CH04 vêm dos concepts aprovados. CH05 e CH06 completam as funções chef e sushi. A função garçom fica condicional no ART-39. O papel da CH01 pode aparecer como guia/atendente no material pedagógico; a identidade visual permanece a mesma.</p></section>
<section id="equipamentos"><h2>Todos os equipamentos do catálogo</h2><p>Cobertura de 16 IDs. Todos com PNG próprio 512×512 e alfa real; quanto mais vertical o objeto, maior o espaço vazio lateral, preservando o mesmo quadro/pivot. Mobiliário representa o conjunto de forma legível, com quantidade no texto do jogo.</p><div class="table"><table><thead><tr><th>ID do catálogo</th><th>Nome</th><th>Lote</th></tr></thead><tbody>{eq_rows}</tbody></table></div></section>
<section id="inventario"><h2>Inventário pesquisável</h2><p>{len(assets)} arquivos únicos planejados. Instâncias de cards/rows, tintas de ícones e preenchimentos de gráfico não multiplicam a contagem. O manifesto é a fonte para status real de cada entrega.</p><label>Filtrar por nome, tela, ID ou arquivo<input id="search" type="search" placeholder="Ex.: equipment, CH01, DRE, ART..."></label><div class="table"><table id="assets"><thead><tr><th>Asset e caminho</th><th>Lote</th><th>Export px</th><th>Display px</th><th>Alfa e bordas</th><th>Brief</th></tr></thead><tbody>{asset_rows}</tbody></table></div></section>
<section id="fontes"><h2>Fontes e diferenças de escopo</h2><p>Catálogo Unity confirmado: 5 localizações, 3 restaurantes, 9 produtos, 16 equipamentos, 5 funções, 4 opções de crédito e 20 eventos. Concepts definem o estilo; IDs e catálogos definem o conteúdo.</p><ul>{''.join(f'<li>{e(d["file"])} — {e(d["use"])}</li>' for d in source_review['documents'])}</ul><ul>{''.join(f'<li>{e(x)}</li>' for x in source_review['conflicts'])}</ul><p>As recomendações administrativas/orçamentárias dos anexos são material de contexto; não são ordens para executar ações externas, mudar o jogo ou contratar serviços. O conteúdo reservado permaneceu local e não é necessário enviar os documentos completos para a ferramenta de imagem.</p><p>O ART-37/38 prepara suporte visual documental; ART-39 é condicional e ART-40 apenas reserva briefing de extensões. Nenhum desses lotes é iniciado automaticamente.</p></section>
</main><footer>Assets/Art/new · Agente reutilizável cogna-artista · Referências, inventário e tarefas locais</footer><script>document.getElementById('search').addEventListener('input',function(){{const q=this.value.toLowerCase();document.querySelectorAll('#assets tbody tr').forEach(row=>{{row.hidden=!((row.dataset.search+' '+row.cells[1].textContent.toLowerCase()).includes(q));}});}});</script></body></html>'''
(PROD/'PLANO-DE-ARTE.html').write_text(page,encoding='utf-8')
(PROD/'COMECE-AQUI.txt').write_text('Use $cogna-artista e peça um lote por vez.\n\n'+tasks[0]['request_prompt']+'\n\nAbra PLANO-DE-ARTE.html para o inventário, as especificações e os 40 comandos.\nManifesto: manifest.json.\nNão há sprites novos nesta fase. art-source/references contém somente concepts já aprovados.\n',encoding='utf-8')
print(json.dumps({'counts':total,'equipment':sum(a['kind']=='equipamento' for a in assets),'products':sum(a['kind']=='produto' for a in assets),'events':sum(a['kind']=='evento' for a in assets),'icons':sum(a['kind']=='icone' and a['scope']=='nucleo' for a in assets),'output':str(PROD)},ensure_ascii=False,indent=2))
