"""Finalize ART-14 after visual review. No scene, identity master or other lot changes."""
from pathlib import Path
import json,hashlib,argparse
from PIL import Image
R=Path(__file__).resolve().parents[3]
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def write(p,d):(R/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((R/p).read_bytes()).hexdigest()
def finalize():
 measured=read('docs/art/qa/ART-14-measurements.json');tech=read('docs/art/qa/ART-14-technical.json');plan=read('docs/art/qa/ART-14-plan-validation.json');scope=read('docs/art/qa/ART-14-scope-audit.json')
 assert measured['passed'] and tech['passed'] and plan['passed'] and scope['passed'] and tech['asset_count']==4
 m=read('docs/art/manifest.json');prod=read('docs/art/prompts/ART-14.json')
 for a in m['assets']:
  if a['task_id']!='ART-14':continue
  r=next(r for r in prod['assets'] if r['id']==a['id']);result=next(r for r in measured['assets'] if r['id']==a['id'])
  assert a['status']=='gerado' and a['sha256']==r['sha256']==result['sha256']==sha(r['output_path'])
  if a['character_ids']:
   assert r['canonical_reference']['sha256']==sha(r['canonical_reference']['path'])
   assert result['visible_vertical_padding_at_least_10percent']
  a['status']='validado';a['qa']={'technical':True,'visual':True,'identity':bool(a['character_ids']),'record':'docs/art/qa/ART-14.json',
   'preview':'art-source/previews/ART-14/ART-14-identification-composed.png',
   'notes':'Dimensões, sRGB/alfa, fundos claros/escuros e composição revisados; CH01 mantém rosto, pele, cabelo, roupa e acessórios do master.' if a['character_ids'] else 'Dimensões, sRGB/alfa, contornos, área do formulário e composição revisados.'}
  r['status']='validado'
  if r.get('previous_generation'):
   old=r['previous_generation'];old['source_sha256']=sha(old['source_path'])
   with Image.open(R/old['source_path']) as probe:old['source_size_px']=list(probe.size)
   old['reference_sha256']={p:sha(p) for p in old['references']}
 for tasks in [m['tasks'],read('docs/art/tasks.json')]:
  next(t for t in tasks if t['id']=='ART-14')['status']='validado'
  if tasks is not m['tasks']:write('docs/art/tasks.json',tasks)
 measured.update({'visual_qa_required':False,'visual_qa_completed':True});write('docs/art/qa/ART-14-measurements.json',measured)
 prod.update({'status':'validado','summary':measured['summary'],'qa_record':'docs/art/qa/ART-14.json',
  'layout_record':'docs/art/layouts/identification-layout.json','build_source_sha256':sha('art-source/editable/ART-14/export-art14.cjs'),
  'lossless_optimizer_sha256':sha('art-source/editable/ART-14/optimize-lossless.cjs')})
 write('docs/art/manifest.json',m);write('docs/art/prompts/ART-14.json',prod)
 qa={'task_id':'ART-14','date':'2026-10-14','status':'validado','reviewer':'Codex — inspeção visual e técnica',
  'technical':{'passed':True,'asset_count':4,'report':'docs/art/qa/ART-14-technical.json','plan_report':'docs/art/qa/ART-14-plan-validation.json'},
  'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'previews':measured['previews'],
   'checks':['Fachada contemporânea brasileira acolhedora, faixa e toldo roxos sem letras; espaço livre de objetos à direita para formulário separado.',
    'Cenário completo e opaco conforme manifesto; personagem e dois ornamentos com alfa verdadeiro fora das silhuetas.',
    'CH01 preserva face, olhos castanhos, pele morena, bob curto ondulado, argolas douradas, camisa lilás, calça/cinto/sapatos grafite e proporções adultas do master.',
    'Prancheta vazia, mão de apoio e gesto de acolhimento legíveis; cabeça, duas mãos e dois pés completos. Sem aura aparente sobre fundos claro/escuro.',
    'Margens verticais visíveis da personagem maiores que 10%; tamanho exportado exato, sem esticar e sem limpeza de alfa por código.',
    'Ornamentos discretos em lavanda/violeta com SVGs separados e geometria espelhada; contornos antialias revisados sobre dois fundos.',
    'Montagem separa cenário, pessoa, ornamentos e componentes reutilizados ART-03/04/05. Textos aparecem somente na prova; não estão nos quatro PNGs finais.',
    'Identificação sem caixa, score ou mês antes da sessão. Nenhuma interface ou cena Unity implementada.',
    'Acabamento do cenário foi ajustado via ImageGen; recompressão PNG posterior comprovou igualdade de todos os bytes RGBA.']},
  'identity':{'passed':True,'character_id':'CH01','canonical_path':'Assets/Art/new/characters/CH01/ch01_canonical.png',
   'canonical_sha256':sha('Assets/Art/new/characters/CH01/ch01_canonical.png'),'comparison':'art-source/previews/ART-14/CH01-identity-comparison.png'},
  'assets':measured['assets'],'summary':measured['summary'],'layout_record':'docs/art/layouts/identification-layout.json',
  'measurements':'docs/art/qa/ART-14-measurements.json','scope_audit':'docs/art/qa/ART-14-scope-audit.json','pending':[],'integration_performed':False}
 write('docs/art/qa/ART-14.json',qa)
 names={'ID_ENVIRONMENT':'Cenário da fachada','ID_ORNAMENT_LEFT':'Ornamento esquerdo','ID_ORNAMENT_RIGHT':'Ornamento direito','CH01_WELCOME':'CH01 com prancheta'}
 lines=['# ART-14 — Arte da identificação','',
  'Quatro PNGs separados, no estilo dos concepts aprovados: cenário de fachada/bairro, dois ornamentos roxos discretos e a CH01 em pé com prancheta e gesto natural de acolhimento. Título, Nome, RA, erro e Cadastrar permanecem conteúdo nativo da interface.','',
  '| Asset | PNG em Assets/Art/new | Export (px) | Arquivo | Alfa |','| --- | --- | --- | --- | --- |']
 for r in prod['assets']:
  w,h=r['actual_size_px'];size=format(r['output_bytes']/1000000,'.2f').replace('.',',')+' MB' if r['output_bytes']>1000000 else format(r['output_bytes']/1000,'.1f').replace('.',',')+' KB'
  lines.append('| '+names[r['id']]+' | ['+Path(r['output_path']).name+'](../../'+r['output_path']+') | '+str(w)+'×'+str(h)+' | '+size+' | '+('Opaco, RGBA' if r['id']=='ID_ENVIRONMENT' else 'Recorte transparente')+' |')
 s=measured['summary'];lines += ['',
  'Os PNGs somam **'+format(s['png_bytes']/1000000,'.2f').replace('.',',')+' MB**. O cenário de tela inteira ocupa '+format(s['environment_bytes']/1000000,'.2f').replace('.',',')+' MB, redução de **'+format(s['environment_reduction_percent'],'.1f').replace('.',',')+'%** ante o primeiro export de 2,78 MB. O acabamento mais uniforme foi feito pelo ImageGen, mantendo composição e arquitetura. A CH01 e os dois ornamentos somam **'+format(s['cutouts_bytes']/1000,'.1f').replace('.',',')+' KB**.','',
  'A resolução do cenário permanece 1920×1080 conforme o manifesto, pois a ilustração preenche toda a tela. Não foi tratado como construção isolada do mapa. A recompressão PNG usa filtros escolhidos por tamanho com igualdade comprovada de todos os bytes RGBA; não houve redução de paleta, conversão JPEG ou remoção de alfa por código. O tamanho de build e a memória real dependem dos ajustes de import/plataforma da integração.','',
  'Cenário e personagem produzidos por ImageGen integrado, um asset por chamada. O modelo não é exposto pela ferramenta e está registrado como `null`. A versão anterior do cenário e os prompts de cada etapa foram preservados fora do Unity. Ornamentos produzidos em SVG determinístico, com rasterização direta em RGBA sRGB.','',
  '[SVG do ornamento esquerdo](../../art-source/editable/identification/ornaments/arc_left.svg) · [SVG do ornamento direito](../../art-source/editable/identification/ornaments/arc_right.svg)','',
  'A nova pose usa o master canônico da CH01, com SHA256 registrado. O master e `characters.json` permanecem intactos. A redução uniforme de 7/8 e o padding transparente enquadram a pessoa com margens verticais visíveis maiores que 10%, sem recortar mãos, cabeça ou pés.','',
  'Todos os PNGs têm pivot `[0.5,0.5]` e bordas zero. Usar escala uniforme: ornamentos cabem na caixa 320×860 como 320×640; a textura inteira da CH01 cabe na caixa 420×760 como 420×630. As caixas não devem ser usadas para esticar cada eixo.','',
  '[Proposta de montagem e posições](layouts/identification-layout.json) · [Camadas de arte montadas](../../art-source/previews/ART-14/ART-14-art-layers.png) · [Prova da identificação com componentes reutilizados](../../art-source/previews/ART-14/ART-14-identification-composed.png)','',
  'A composição de referência registra uma alternativa de encaixe uniforme pela silhueta visível da CH01 para aproximar sua presença em primeiro plano do concept. Nesse caso, a textura inteira é desenhada como 654×981 na posição `[15,57]`, com escala comum `654/1024`; a silhueta ocupa aproximadamente a caixa `[170,174,420,760]`. Nenhum pixel de alfa foi removido. A montagem não aplica esses valores a cenas.','',
  'A prova reutiliza painel, campos, botão e barra já validados nos ART-03/04/05. Os nomes e o botão Cadastrar aparecem apenas como simulação de texto nativo na prévia. A identificação não exibe caixa, score ou mês antes da sessão.','',
  '[Comparação de identidade](../../art-source/previews/ART-14/CH01-identity-comparison.png) · [Mãos e prancheta](../../art-source/previews/ART-14/CH01-welcome-hands-clipboard.png) · [CH01 nos dois fundos](../../art-source/previews/ART-14/CH01_WELCOME-display-light-dark.png) · [Ornamento esquerdo nos dois fundos](../../art-source/previews/ART-14/ID_ORNAMENT_LEFT-display-light-dark.png) · [Ornamento direito nos dois fundos](../../art-source/previews/ART-14/ID_ORNAMENT_RIGHT-display-light-dark.png)','',
  '[Prompts exatos, referências, fontes e SHA256](prompts/ART-14.json) · [Exportador técnico](../../art-source/editable/ART-14/export-art14.cjs) · [Recompressão PNG lossless](../../art-source/editable/ART-14/optimize-lossless.cjs) · [QA](qa/ART-14.json) · [Medições](qa/ART-14-measurements.json) · [Auditoria](qa/ART-14-scope-audit.json)','',
  'Validação dos quatro PNGs e do plano aprovada. Dimensões, RGBA sRGB, alfa, margens, fontes SVG, identidade e montagem revisados. Geometria dos ornamentos espelhada no SVG, com variações subpixel de antialias sem alteração perceptível.','',
  'Pendências de produção: nenhuma. Integração às cenas fica para etapa própria. Outros assets, fontes, identidades, cenas, scripts, catálogos e contratos do mapa foram preservados.']
 (R/'docs/art/ART-14-entregas.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 print(json.dumps({'task_id':'ART-14','status':'validado','png_count':4,'svg_count':2,'png_bytes':s['png_bytes'],'pending':[]}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--visual-reviewed',action='store_true');a=p.parse_args()
 if not a.visual_reviewed:p.error('Concluir revisão visual antes de validar.')
 finalize()
