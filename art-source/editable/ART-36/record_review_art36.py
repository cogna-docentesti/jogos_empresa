"""Record the direct review completed in the ART-36 chat; no image generation.

Run after audit_art36.py and preview_art36.py. The fixed list below corresponds
to pages actually opened with view_image, not a discovery-based approval.
"""
from pathlib import Path
import collections
import hashlib
import json
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[3]
QA=ROOT/'docs/art/qa'
BASE='5d18996a6ff6cebc5e6dab0c04eaef4d5e93e107'

def read(rel):return json.loads((ROOT/rel).read_text(encoding='utf-8'))
def save(rel,value):(ROOT/rel).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(rel):return hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()

reviewed=[f'ART-36-display-{i:02d}.png' for i in range(1,50)]
reviewed += [f'ART-36-full-{aid}-{theme}.png' for aid in ['ui_topbar_violet','ui_navbar_plum','ui_scrim','id_environment','map_base'] for theme in ['light','dark']]
reviewed += [f'ART-36-map-map_rest_{r}-{t}.png' for r in ['frances','japones','lanches'] for t in ['light','dark']]
reviewed += [f'ART-36-map-master-overlay_{x}.png' for x in ['condominium','marketing','center']]
reviewed += ['ART-36-dre-monthly.png','ART-36-dre-quarterly.png']
reviewed += [f'ART-36-native-{a}.png' for a in ['ch01_canonical','ch02_canonical','ch03_canonical','ch04_canonical','ch05_canonical','ch06_canonical','ch01_welcome','ch01_bank','portrait_attendant','portrait_grill_cook','portrait_head_chef','portrait_manager','portrait_sushi_chef','ch01_dre','ch01_discussing','ch04_discussing','ch01_concerned']]
assert len(reviewed)==87
index=read('docs/art/qa/ART-36-preview-index.json')
assert {Path(p['path']).name for p in index['pages']}==set(reviewed)
for page in index['pages']:
    assert sha(page['path'])==page['sha256'],page['path']
    page['directly_inspected']=True
index['review_status']='inspecionado_diretamente'
index['review_tool']='functions.view_image; all 87 pages opened in the ART-36 chat'
save('docs/art/qa/ART-36-preview-index.json',index)
external=[
 'art-source/previews/ART-14/ART-14-identification-composed.png',
 'art-source/previews/ART-25/ART-25-layer-proof.png',
 'art-source/previews/ART-26/ART-26-layer-proof.png',
 *[f'art-source/previews/ART-27/ART-27-{r}-1920x1080.png' for r in ['frances','japones','lanches','revisao-pendente']],
]
observations={
 'ART-01':'CH01 bob/argolas/camisa lilás; CH02 boné/barba/avental; CH03 cabelo longo/blazer; CH04 óculos/barba/tricô marinho. Cabeças, mãos e pés íntegros; recortes claros nos dois fundos.',
 'ART-02':'CH05 coque alto/pele escura/avental ameixa e CH06 cabelo curto/sem barba/avental grafite mantêm identidades distintas. Contornos limpos e silhuetas adultas.',
 'ART-03':'Painéis e cards mantêm cantos em 9-slice. Sombra e scrim são peças de composição. Logo oficial perde contraste direto em ameixa: usar superfície marfim; não recolorir o master.',
 'ART-04':'Cinco famílias com estados visualmente distintos por cor; bounds, pivots e alfa idênticos. Sem deslocamento desenhado entre estados.',
 'ART-05':'Campos de foco/erro preservam caixa; espessura de contorno pode mudar sem deslocamento. Radio/checkbox/toggle são bases; sinais, thumb e interações permanecem separados. Trilhos e scrollbar sem estiramento de cantos.',
 'ART-06':'18 glifos de navegação legíveis em 40 px e reconhecíveis no teste óptico de 20 px; branco no escuro, tint ameixa só na prova clara.',
 'ART-07':'18 indicadores financeiros distinguíveis em 40 px; linhas e vazios limpos nos dois fundos.',
 'ART-08':'33 glifos de ações/status/categorias distinguíveis. Lock, alert, info, som e offline mantêm vazios internos; miniaturas são leitura óptica, não alvos de toque.',
 'ART-09':'Pills, KPI, tabelas e toasts com bordas preservadas; trilho e preenchimento separados. Dados e gráficos continuam nativos. Linha de navegação de 4 px fica fina a 50%, como previsto pela redução.',
 'ART-10':'Geografia transparente fora do terreno, com rio/duas pontes preservados; desenho 1280x720 centralizado no viewport 1280x848 por escala 2/3.',
 'ART-11':'Cinco landmarks encaixam nos lotes, com luz/perspectiva coerentes e sem construção sobre vias. Texturas compactas usam offsets individuais.',
 'ART-12':'Três restaurantes alternam no mesmo terreno, entrada e ordem; foreground recobre a frente sem deslocar porta. Apenas uma variante por montagem.',
 'ART-13':'Cinco realces e centro respeitam os polígonos; centro não adiciona Location. Pinos/rótulos preservam silhueta nos estados e mantêm símbolos/textos independentes.',
 'ART-14':'Cenário com área livre para formulário; CH01 mantém bob e argolas. Composição anterior conferida: formulário e personagem separados; sem HUD econômico antes da sessão.',
 'ART-15':'Fachadas francesa/japonesa/lanches compartilham forma externa e distinguem interiores. Alfa sem fundo incorporado; leitura mantida a 480x360 e 240x180.',
 'ART-16':'Cinco vinhetas de localidades coerentes com landmarks; árvores e piso permanecem na vinheta independente, sem nomes rasterizados.',
 'ART-17':'Três combos de lanches distinguíveis por composição. Louça/bandeja completas e contornos legíveis em 250/125 px.',
 'ART-18':'Três produtos japoneses com composições próprias, sem receita ou quantidade dinâmica inferida. Recortes e alimento legíveis.',
 'ART-19':'Três produtos franceses preservam a composição visual aprovada do lote; pratos e contornos íntegros.',
 'ART-20':'Quatro equipamentos de cozinha com perspectiva coerente e portas/suportes completos. Detalhes de metal não se confundem com halo de fundo.',
 'ART-21':'Freezer, geladeira, mobiliário e PDV distintos; bases completas. Peças escuras perdem contraste em ameixa e devem ocupar cards claros no jogo.',
 'ART-22':'Chapa e fritadeira dupla reconhecíveis nas duas escalas; não há estado adquirido/bloqueado redesenhado no bitmap.',
 'ART-23':'Balcão, facas e panela de arroz distintos; vidro e lâminas mantêm separação de fundo.',
 'ART-24':'Adega, forno e mobiliário de luxo completos e distintos. Pernas escuras do mobiliário exigem card claro na integração.',
 'ART-25':'CH01 coerente com canônico; quatro créditos com símbolos próprios. Banco é ambiente opaco independente. Prova anterior personagem/ambiente também inspecionada.',
 'ART-26':'Cinco retratos comparados aos respectivos canônicos em cortes nativos; cabelo/pele/figurino/acessórios preservados. Cinco ambientes e montagem conjunta inspecionados.',
 'ART-27':'Três estabelecimentos e revisão pendente existentes conferidos diretamente; fachadas, retratos, checklist e botão compostos com peças independentes. Valores são marcadores de QA.',
 'ART-28':'DRE mensal: papel atrás dos dedos, pessoa atrás da mesa, caneca/calculadora acima do tampo. Recorte comum 560x420 e contato mão/folha preservados.',
 'ART-29':'DRE trimestral: CH01 olha para CH04, óculos/barba/bob/argolas preservados. Mãos legíveis; mesa oculta coxas. Mesmos props e viewport da mensal.',
 'ART-30':'Cinco vinhetas de estoque/fornecedor distinguíveis por caixas, etiqueta, relógio, balança ou desperdício. Rótulos vazios e recortes limpos.',
 'ART-31':'Cinco vinhetas de demanda/concorrência distinguíveis; sem pessoas novas ou opções fixadas no raster. Fachadas e props coerentes com repertório.',
 'ART-32':'Cinco situações de operação/equipe distinguíveis por freezer, inspeção, fila de louça, fluxo de produção e cabo solto de pagamento. Sem texto de teste no raster.',
 'ART-33':'Cinco situações de comunicação/contexto distinguíveis; telas em branco, estrelas inteiras/quebradas e chuva legíveis. Chuva sem fundo retangular incorporado.',
 'ART-34':'Conclusão e dificuldade distinguíveis por props/ambiente, sem troféu infantil. Três vazios com campos em branco. CH01 preocupada preserva identidade e expressão contida.',
 'ART-35':'Spinner aberto, tap, drag e focus ring distintos em 48/64 px e miniaturas. Centro dos anéis transparente. Animação/visibilidade ainda são trabalho de UI.',
}
inventory=read('docs/art/qa/ART-36-inventory.json')
contracts=read('docs/art/qa/ART-36-contracts.json')
integration=read('docs/art/qa/ART-36-integration-map.json')
assert inventory['passed'] and contracts['passed'] and not integration['errors']
for selector,name in [(['--task','ART-36'],'ART-36-technical.json'),(['--plan'],'ART-36-plan-validation.json')]:
    command=[sys.executable,str(ROOT/'tools/art/validate_assets.py'),'--project',str(ROOT),*selector]
    result=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',check=True)
    report=json.loads(result.stdout);assert report['passed']
    save('docs/art/qa/'+name,report)
save('docs/art/prompts/ART-36.json',{
 'task_id':'ART-36','date':'2026-10-15','production_method':'audit_and_existing_asset_composition',
 'exact_instruction':read('docs/art/manifest.json')['tasks'][35]['note'],
 'source_task':'docs/art/tasks/ART-36.txt','source_task_sha256':sha('docs/art/tasks/ART-36.txt'),
 'generation_tool':None,'generation_model':None,'imagegen_used':False,'new_sprites':0,
 'reason':'ART-36 exige auditoria e provas; nenhuma ilustração criada ou alterada.',
 'tools':['Python 3.10','Pillow','PyYAML','tools/art/validate_assets.py','functions.view_image'],
 'reproducible_sources':['art-source/editable/ART-36/audit_art36.py','art-source/editable/ART-36/preview_art36.py'],
 'asset_origins_and_exact_prior_prompts':'docs/art/qa/ART-36-inventory.json',
 'reference_policy':'Concepts são referência visual; somente os PNGs finais do manifesto compõem as provas.'})
qa={
 'task_id':'ART-36','date':'2026-10-15','status':'validado','reviewer':'Codex; inspeção direta por view_image na conversa ART-36',
 'base_commit':BASE,'scope':'Auditoria final e mapa de integração; zero sprites novos; nenhuma cena/metadado alterado.',
 'technical':{'passed':True,'asset_count':265,'tasks_revalidated':35,'production_pngs_in_this_task':0,
    'all_png_sizes_rgba_alpha_hashes_passed':True,'canonical_hashes_matched':6,
    'reports':['docs/art/qa/ART-36-technical.json','docs/art/qa/ART-36-plan-validation.json','docs/art/qa/ART-36-prerequisites.json',
      'docs/art/qa/ART-36-inventory.json','docs/art/qa/ART-36-contracts.json','docs/art/qa/ART-36-scope-audit.json'],
    'validator_limit':'--task ART-36 valida zero sprites. A cobertura dos 265 vem da revalidação explícita ART-01..35 e da auditoria de hashes/contratos.'},
 'visual':{'passed':True,'new_pages_inspected':87,'page_size_px':[1920,1080],
    'preview_index':'docs/art/qa/ART-36-preview-index.json','backgrounds':['#F5F0E8','#24113D'],
    'scales':['100% das caixas do manifesto com contain/cover/9-slice','50% para stress óptico','cortes nativos 1:1 de rosto/mãos e mapa'],
    'mobile_limit':'Redução óptica; não equivale a teste de UI responsiva ou interação móvel.',
    'inspected_previews':[p['path'] for p in index['pages']],
    'prior_assemblies_reinspected':[{'path':p,'sha256':sha(p)} for p in external],
    'approved_concepts_inspected':[{'path':f'art-source/references/concepts/{n}.png','sha256':sha(f'art-source/references/concepts/{n}.png')} for n in ['01-hub','02-abertura','03-gestao','04-dre-mensal','05-dre-trimestral']],
    'observations_by_task':observations,
    'assets':[{'id':a['id'],'directly_inspected':True,'evidence':index['coverage'][a['id']],
               'observation':observations[a['task_id']]} for a in inventory['assets']]},
 'integration_performed':False,'runtime_tested':False,'integration_ready':False,
 'integration_report':'docs/art/qa/ART-36-integration-map.json',
 'pending':[
    {'kind':'future_integration','detail':'227 assets têm propostas de importação pendentes: 207 em Multiple, 67 com bordas diferentes, 16 com pivot diferente, 227 com compressão na plataforma padrão. Grupos se sobrepõem. Preservar GUIDs e revisar fileIDs antes de migrar.'},
    {'kind':'future_integration','detail':'Nenhum dos 265 GUIDs aparece nos 175 arquivos serializados varridos. Vincular sprites aos alvos do integration-map, sem trocar IDs/economia.'},
    {'kind':'future_integration','detail':'Uniformizar CanvasScalers 1280x720 e 800x600 com a proposta 1920x1080; validar layout e input no Unity/plataformas depois. Nenhum teste de runtime foi executado.'},
    {'kind':'usage_condition','detail':'Logo oficial e objetos escuros precisam de superfície clara; overlays exigem base; ícones brancos precisam de tint adequado. Textos, valores e gráficos continuam separados.'}],
 'audit_pending':[],'new_sprite_count':0,'production_record':'docs/art/prompts/ART-36.json',
 'integration_status_semantics':'validado refere-se à auditoria e às provas. Não significa importação configurada ou UI integrada.'}
save('docs/art/qa/ART-36.json',qa)
for file in ['docs/art/manifest.json','docs/art/tasks.json']:
    data=read(file);tasks=data['tasks'] if isinstance(data,dict) else data
    task=next(t for t in tasks if t['id']=='ART-36')
    task.update(status='validado',qa_record='docs/art/qa/ART-36.json',production_record='docs/art/prompts/ART-36.json',
                delivery_preview='art-source/previews/ART-36/ART-36-map-map_rest_frances-dark.png',
                integration_map='docs/art/qa/ART-36-integration-map.json',validation_asset_count=265,new_sprite_count=0)
    if isinstance(data,dict):
        data['phase']='produção por lotes; ART-01 a ART-36 validados; integração no jogo pendente'
    save(file,data)
# Friendly local gallery; images remain individual review files, outside Unity.
import html
gallery='''<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>ART-36 — provas auditadas</title>
<style>body{margin:32px;background:#24113d;color:#f5f0e8;font:18px Arial}a{color:#b99ae8}figure{margin:36px 0}img{display:block;width:100%;max-width:1920px;height:auto;background:#f5f0e8}figcaption{padding:12px 0}</style>
<h1>ART-36 — 87 provas, 265 assets existentes</h1><p>Auditoria visual concluída. Integração e testes no Unity pendentes. Miniaturas a 50% são stress óptico.</p>'''
for page in index['pages']:
    name=Path(page['path']).name
    gallery+=f'<figure><a href="{html.escape(name)}"><img loading="lazy" src="{html.escape(name)}" alt="{html.escape(name)}"></a><figcaption>{html.escape(name)} · {html.escape(", ".join(page["asset_ids"]))}</figcaption></figure>'
(ROOT/'art-source/previews/ART-36/index.html').write_text(gallery+'\n</html>\n',encoding='utf-8')
print(json.dumps({'task':'ART-36','status':'validado','assets_reviewed':265,'new_sprites':0,'pages':87,'integration_ready':False}))
