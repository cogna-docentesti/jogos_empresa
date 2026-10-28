"""Record the completed Codex visual review of ART-29 after inspecting its previews."""
from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[3]
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def write(p,d):(ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def main():
    for report in ['technical','plan-validation','prerequisites','contract-validation','scope-audit']:
        assert read('docs/art/qa/ART-29-'+report+'.json')['passed']
    p=read('docs/art/prompts/ART-29.json');m=read('docs/art/manifest.json');c=read('docs/art/layouts/dre-layout.json');tasks=read('docs/art/tasks.json')
    notes={
        'CH01_DISCUSSING':'Identidade comparada ao master: rosto adulto, pele, olhos castanhos, bob ondulado curto com risca lateral, argolas douradas e camisa lilás preservados. Pose sentada, olhar para a direita, mão junto ao queixo e pinça vazia; mãos legíveis e contorno limpo em fundos claro/escuro. Papel e móveis permanecem sprites independentes.',
        'CH04_DISCUSSING':'Identidade comparada ao master: rosto adulto, pele, cabelo castanho ondulado, barba curta, óculos de armação escura, suéter azul-marinho e gola lilás preservados. Pose sentada voltada à esquerda, gesto explicativo e mão de apoio completos. Revisão elevou o antebraço e baixou as coxas; o tampo oculta a parte inferior sem cortar os dedos.'
    }
    for g in p['generations']:
        refs=[('edit target - previous pose','art-source/editable/ART-29/generations/ch04_discussing.png')] if g.get('revision_reason') else [('canonical identity master',g['references'][0]),('approved quarterly style and orientation concept',g['references'][1])]
        g['input_images']=[{'role':role,'path':ref,'sha256':sha(ref)} for role,ref in refs]
    for a in m['assets']:
        if a['task_id']=='ART-29':
            a.update(status='validado',qa={'technical':True,'visual':True,'identity':True},qa_record='docs/art/qa/ART-29.json')
    next(t for t in m['tasks'] if t['id']=='ART-29')['status']='validado'
    next(t for t in tasks if t['id']=='ART-29')['status']='validado'
    for r in p['assets']:r.update(status='validado',visual_notes=notes[r['id']])
    p.update(status='validado',qa_record='docs/art/qa/ART-29.json',contract='docs/art/layouts/dre-layout.json',contract_view='quarterly',scope_resolution='Somente ART-29; o encadeamento autorizado abre a próxima conversa apenas após commit e conclusão pelo helper.',reference_policy='Masters and approved concept inspected before generation. Three integrated ImageGen results preserved byte-for-byte; initial mentor pose rejected for tabletop alignment, corrected revision selected. Both final sprites are independent files; no previous-lot art regenerated.')
    c['quarterly']['status']='validado'
    qa={
        'task_id':'ART-29','title':'Personagens da DRE trimestral','date':'2026-10-15','status':'validado','reviewer':'Codex — inspeção visual e técnica',
        'production':{'tool':'image_gen.imagegen','model':None,'model_note':p['model_note'],'distinct_asset_generations':2,'revision_generations':1,'prompts_record':'docs/art/prompts/ART-29.json','source_directory':'art-source/editable/ART-29/generations','export_source':p['producer_source'],'preview_source':p['preview_source']},
        'technical':{'passed':True,'asset_count':2,'report':'docs/art/qa/ART-29-technical.json','plan_report':'docs/art/qa/ART-29-plan-validation.json','prerequisite_report':'docs/art/qa/ART-29-prerequisites.json','schema_and_alignment_report':'docs/art/qa/ART-29-contract-validation.json','scope_report':'docs/art/qa/ART-29-scope-audit.json'},
        'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'scales':'Dois assets em tamanho nativo 768x1024 e exibição 360x480, cada um sobre fundos claro e escuro. Prova conjunta 560x420, posicionamento no canvas 1920x1080 e comparação dos dois personagens aos masters.','inspected_previews':p['visual_review_files'],'checks':[
            'Uma pessoa por PNG; sem conversa achatada, props incorporados, texto, números, dados, gráficos, tabela ou UI.',
            'Identidades CH01 e CH04 comparadas aos masters canônicos com hashes preservados. Rostos, pele, olhos, cabelos, acessórios, figurinos e proporções adultos mantidos.',
            'Mãos, dedos, expressão, silhueta, contornos, perspectiva e luz superior esquerda inspecionados em fundos claro/escuro e na montagem.',
            'Alfa verdadeiro, sem checkerboard, cenário ou halo amplo incorporado. Alfa original do ImageGen preservado, com antialias visível em ambos os fundos.',
            'As fontes retornaram 1086x1448; export técnico uniforme com padding transparente entrega 768x1024 exatos e pelo menos 10% de margem. Sem crop do arquivo final ou esticamento x/y.',
            'Primeira pose CH04 reprovada na montagem por mão baixa e coxas acima do tampo; revisão pelo ImageGen mantém identidade, eleva o antebraço e baixa as coxas. Original, revisão, prompts e hashes preservados.',
            'Papel independente fica atrás da pinça de CH01; mesa fica acima das duas pessoas e oculta a parte inferior. Mãos completas visíveis; âncoras explícitas do mentor. Z-order basta, sem máscara adicional.',
            'Cenário, mesa, folha, xícara e calculadora da ART-28 reutilizados sem alteração de PNGs ou metadados. Contrato mensal original preservado; prova mensal idêntica em bytes e pixels.',
            'Montagens achatadas existem somente nos previews de QA. Os sprites finais permanecem separados e nenhuma integração no Unity foi executada.'
        ]},
        'composition':{'contract':'docs/art/layouts/dre-layout.json','contract_view':'quarterly','schema':'docs/art/schemas/dre-layout.schema.json','canvas_px':[1920,1080],'viewport_px':c['quarterly']['viewport_px'],'paper_hand_distance_px':0,'mentor_hand_to_desk_back_edge_y_distance_px':2.671875,'occlusion_masks_required':False,'monthly_contract_preserved':True,'monthly_composite_png_and_pixels_identical':True,'proof':p['delivery_preview']},
        'assets':[{'id':r['id'],'path':r['output_path'],'status':'validado','actual_size_px':r['actual_size_px'],'mode':r['mode'],'sha256':r['sha256'],'source_path':r['source_path'],'source_sha256':r['source_sha256'],'source_size_px':r['source_size_px'],'canonical_identity':r['canonical_identity'],'references':r['references'],'reference_sha256':r['reference_sha256'],'alpha':r['measurements'],'technical_export':r['technical_export'],'technical':True,'visual':True,'identity':True,'notes':notes[r['id']]} for r in p['assets']],
        'previews':[{'path':ref,'sha256':sha(ref),'preview_only':True} for ref in p['visual_review_files']],
        'pending':[],'future_integration':'Aplicação de sprites e dados dinâmicos em cenas está fora do lote. Consumir quarterly para o trimestre; os campos mensais anteriores permanecem na raiz.','integration_performed':False
    }
    write('docs/art/manifest.json',m);write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-29.json',p);write('docs/art/layouts/dre-layout.json',c);write('docs/art/qa/ART-29.json',qa)
    print('ART-29 visual review recorded; task and two assets validated.')
if __name__=='__main__':main()
