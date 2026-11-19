"""Record the completed human-visible review after inspecting the listed previews."""
from pathlib import Path
import json,hashlib,subprocess,sys
ROOT=Path(__file__).resolve().parents[3]
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
def write(p,d):(ROOT/p).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def main():
    p=read('docs/art/prompts/ART-28.json');m=read('docs/art/manifest.json');c=read('docs/art/layouts/dre-layout.json')
    for report in ['technical','plan-validation','prerequisites','contract-validation','scope-audit']:
        assert read('docs/art/qa/ART-28-'+report+'.json')['passed']
    concept='art-source/references/concepts/04-dre-mensal.png'
    master='Assets/Art/new/characters/CH01/ch01_canonical.png'
    for g in p['generations']:
        refs=[('edit target','art-source/editable/ART-28/generations/env.png')] if g['key']=='env_revision' else ([('canonical identity master',master),('approved style and pose concept',concept)] if g['key']=='ch01' else [('approved concept',concept)])
        g['input_images']=[{'role':role,'path':ref,'sha256':sha(ref)} for role,ref in refs]
    notes={
        'DRE_ENVIRONMENT':'Janela com toldo roxo sem letreiro, escritório frontal, luz superior esquerda, sem pessoas internas/externas, mesa frontal, dados ou UI. Livros e plantas posteriores são ambientação fixa.',
        'DRE_DESK':'Mesa vazia completa, pernas e tampo independentes, sem pessoa ou props incorporados. Contornos limpos nos fundos claro/escuro.',
        'CH01_DRE':'Identidade comparada ao master: rosto, pele, olhos, bob curto ondulado, risca lateral, argolas douradas e camisa lilás preservados. Pose sentada, mãos legíveis; corte intencional nas coxas oculto pela mesa. Sem papel ou móveis incorporados.',
        'DRE_PROP_CALCULATOR':'Calculadora legível a 96x96, teclas e visor completamente sem números/símbolos, perspectiva compatível com o tampo.',
        'DRE_PROP_COFFEE_CUP':'Xícara marfim sem estampa, café visível, alça direita com vão transparente, base assentada sobre o tampo na prova.',
        'DRE_PROP_REPORT_BLANK':'Folha em branco sem linhas/gráficos/dados; contorno completo, borda direita coincide com a mão; dedos ocultam a borda pelo z-order.'
    }
    for a in m['assets']:
        if a['task_id']=='ART-28':
            a['status']='validado';a['qa']={'technical':True,'visual':True,'identity':True if a['character_ids'] else None};a['qa_record']='docs/art/qa/ART-28.json'
    m['tasks'][next(i for i,t in enumerate(m['tasks']) if t['id']=='ART-28')]['status']='validado'
    tasks=read('docs/art/tasks.json')
    next(t for t in tasks if t['id']=='ART-28')['status']='validado'
    for r in p['assets']:r['status']='validado';r['visual_notes']=notes[r['id']]
    p['status']='validado';p['qa_record']='docs/art/qa/ART-28.json';p['contract']='docs/art/layouts/dre-layout.json'
    p['scope_resolution']='Somente ART-28; autorização da fila é usada apenas para encadear a próxima conversa após o commit.'
    p['reference_policy']='Originals and approved references were inspected before generation. Seven original ImageGen results are preserved byte-for-byte under art-source/editable/ART-28/generations; the first environment is rejected, its corrected revision selected.'
    c['status']='validado'
    previews=[{'path':ref,'sha256':sha(ref),'preview_only':True} for ref in p['visual_review_files']]
    qa={'task_id':'ART-28','title':'Ilustração em camadas da DRE mensal','date':'2026-10-15','status':'validado','reviewer':'Codex — inspeção visual e técnica',
        'production':{'tool':'image_gen.imagegen','model':None,'model_note':p['model_note'],'distinct_asset_generations':6,'revision_generations':1,'prompts_record':'docs/art/prompts/ART-28.json','source_directory':'art-source/editable/ART-28/generations','export_source':p['producer_source'],'preview_source':p['preview_source']},
        'technical':{'passed':True,'asset_count':6,'report':'docs/art/qa/ART-28-technical.json','plan_report':'docs/art/qa/ART-28-plan-validation.json','prerequisite_report':'docs/art/qa/ART-28-prerequisites.json','schema_and_alignment_report':'docs/art/qa/ART-28-contract-validation.json','scope_report':'docs/art/qa/ART-28-scope-audit.json'},
        'visual':{'passed':True,'backgrounds':['#F5F0E8','#24113D'],'scales':'Seis arquivos em tamanho nativo e nas caixas de exibição do manifesto; também montagem 560x420 e comparação CH01/master. Contact sheet é visão geral reduzida; previews display-light-dark têm os tamanhos exatos.','inspected_previews':p['visual_review_files'],'checks':[
            'Seis assets individualizados; nenhum sprite sheet, card, tabela, texto, dado, número ou gráfico incorporado.',
            'Alfa verdadeiro nos cinco recortes, sem checkerboard, halo amplo ou cenário incorporado. O fundo é RGBA completamente opaco. Antialias e alfa retornados pelo ImageGen preservados.',
            'Identidade CH01 comparada ao master canônico de hash d908027ce00f769f51c6added13e0155be6bf7d5b990d17440b425f06fb13c45. Rosto, pele, olhos, cabelo, figurino e argolas preservados; mãos, postura, silhueta e contornos inspecionados.',
            'Fontes geradas têm dimensões diferentes das finais, registradas por arquivo. Redução uniforme via sharp/libvips; CH01 recebeu padding transparente para cumprir a margem de 10%. Sem crop, stretch x/y, pintura ou limpeza de alfa por script.',
            'Primeiro ambiente reprovado por pessoas ao longe e mesa frontal incorporada. Revisão ImageGen removeu ambos. Original e revisão preservados com prompts exatos e hashes.',
            'Montagem usa exatamente as escalas/pivots/posições do contrato: papel atrás dos dedos de CH01, mesa à frente da parte inferior sentada, xícara e calculadora sobre o tampo. Z-order suficiente; não são necessárias máscaras.',
            'Viewport 560x420 recorta intencionalmente pernas da mesa e parte inferior da pose; PNGs finais completos permanecem separados. Prova anterior à mesa expõe essa separação.',
            'Luz suave do alto à esquerda e perspectiva frontal com visão moderada do tampo consistentes entre cenário, pessoa, mesa e props.']},
        'composition':{'contract':'docs/art/layouts/dre-layout.json','schema':'docs/art/schemas/dre-layout.schema.json','canvas_px':[1920,1080],'viewport_px':c['monthly_viewport_px'],'paper_hand_distance_px':0,'occlusion_masks_required':False,'proof':p['delivery_preview']},
        'assets':[{'id':r['id'],'path':r['output_path'],'status':'validado','actual_size_px':r['actual_size_px'],'mode':r['mode'],'sha256':r['sha256'],'source_path':r['source_path'],'source_sha256':r['source_sha256'],'source_size_px':r['source_size_px'],'references':r['references'],'reference_sha256':r['reference_sha256'],'alpha':r['measurements'],'technical_export':r['technical_export'],'technical':True,'visual':True,'identity':True if r['id']=='CH01_DRE' else 'not_applicable','notes':notes[r['id']]} for r in p['assets']],
        'previews':previews,'pending':[],'future_integration':'Integração em cenas fora deste lote. ART-29 acrescentará montagem trimestral e âncoras do mentor sem regenerar estes props.','integration_performed':False}
    write('docs/art/manifest.json',m);write('docs/art/tasks.json',tasks);write('docs/art/prompts/ART-28.json',p);write('docs/art/layouts/dre-layout.json',c);write('docs/art/qa/ART-28.json',qa)
    subprocess.run([sys.executable,str(ROOT/'art-source/editable/ART-28/verify-art28.py')],check=True,cwd=ROOT)
    print('ART-28 QA recorded: validated')
if __name__=='__main__':main()
