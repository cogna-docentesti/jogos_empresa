// Run after native/display, canonical identity and layer-proof inspection.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),cp=require('node:child_process');
const root=path.resolve(__dirname,'../../..'),tid='ART-26';
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const write=(p,d)=>fs.writeFileSync(path.join(root,p),JSON.stringify(d,null,2)+'\n');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex'),equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
const m=read('docs/art/manifest.json'),index=read('docs/art/tasks.json'),p=read('docs/art/prompts/ART-26.json'),baseline=read('docs/art/qa/ART-26-scope-baseline.json'),technical=read('docs/art/qa/ART-26-technical.json'),plan=read('docs/art/qa/ART-26-plan-validation.json'),prereqs=read('docs/art/qa/ART-26-prerequisites.json');
if(!technical.passed||technical.asset_count!==10||!plan.passed||!prereqs.passed||p.status!=='gerado')throw Error('Requires ten generated assets and successful prerequisite/technical/plan QA');
for(const a of p.assets){
 if(sha(a.output_path)!==a.sha256||sha(a.source_path)!==a.source_sha256)throw Error('Changed file '+a.id);
 for(const [ref,hash] of Object.entries(a.reference_sha256))if(sha(ref)!==hash)throw Error('Changed reference '+ref);
 if(a.canonical_identity&&sha(a.canonical_identity.path)!==a.canonical_identity.sha256)throw Error('Canonical changed '+a.id);
}
const checks={
 PORTRAIT_ATTENDANT:'CH01 preservada: rosto, olhos castanhos, pele morena quente, bob curto ondulado com risca lateral, argolas douradas, camisa lilás de mangas dobradas, cinto grafite. Meio corpo com mãos reunidas na cintura, dedos e cotovelos completos. Sem prancheta nem fundo. Corte inferior deliberado abaixo das mãos, com padding transparente.',
 PORTRAIT_GRILL_COOK:'CH02 preservado: pele morena, olhos escuros, barba curta contornada, boné grafite para trás, camiseta branca e avental grafite com bolso liso. Meio corpo de braços cruzados conforme concept de equipe, mão visível natural e cotovelos completos. Sem ferramenta ou fundo.',
 PORTRAIT_HEAD_CHEF:'CH05 preservada: mulher negra adulta de compleição média robusta, rosto oval largo, cabelo crespo em coque alto compacto, dólmã marfim com botões claros, mangas dobradas e avental ameixa. Sem joias/chapéu. Mãos reunidas, dedos completos; rosto e silhueta compatíveis com o master.',
 PORTRAIT_MANAGER:'CH03 preservada e distinta de CH01: rosto, olhos castanhos, pele morena, cabelo LONGO ondulado, argolas douradas, blazer e cintura ameixa sobre blusa marfim. Meio corpo com mãos reunidas na cintura, sem prancheta. Cabelo, mãos, punhos e cotovelos completos.',
 PORTRAIT_SUSHI_CHEF:'CH06 preservado: rosto oval retangular de queixo arredondado, pele morena clara, cabelo preto curto de risca lateral, olhos castanhos escuros, sem barba/óculos/chapéu. Jaqueta marfim transpassada com botões grafite e mangas dobradas; avental grafite com faixa lilás no bolso. Mãos reunidas e completas, identidade distinta de CH02.',
 TEAM_ENV_SERVICE_COUNTER:'Ambiente de atendimento vazio: balcão fixo de madeira com tampo limpo, janela alta à esquerda, paredes marfim e detalhes lavanda, passagem ao fundo para cozinha. Perspectiva frontal e luz superior esquerda coerentes. Sem pessoa, texto, logo ou UI; decoração/utensílios posteriores pertencem ao cenário.',
 TEAM_ENV_GRILL_KITCHEN:'Cozinha de chapa vazia: coifa grafite, chapa lisa de inox sem comida/chama, azulejos claros, prateleiras e trilho de panelas posteriores, bancada frontal integrada vazia. Perspectiva e luz coerentes. Sem pessoas, marcações ou UI. Equipamento arquitetônico integrado é cenário, não duplicata dos sprites de catálogo.',
 TEAM_ENV_MANAGEMENT_OFFICE:'Escritório vazio: janela esquerda, paredes marfim, marcenaria de madeira/lavanda e plantas pequenas de ambientação em prateleiras e peitoril. Versão 02 remove planta solta do primeiro plano e cadeira; piso e região central livres. Sem pessoa, tela, papel, texto, poster, logo ou UI.',
 TEAM_ENV_FRENCH_KITCHEN:'Cozinha francesa vazia: janela alta em arco, pedra/azulejos marfim, fogão e coifa integrados, inox, marcenaria de madeira e panelas de cobre no trilho posterior. A extensão para ambiente de trabalho preserva a linguagem do restaurante francês aprovado. Sem pessoa, prato pronto, texto ou UI.',
 TEAM_ENV_SUSHI_COUNTER:'Balcão de sushi vazio: madeira clara/escura, painéis e armários lavanda, janela quadriculada inspirada em shoji à esquerda, superfície frontal limpa e vitrine posterior vazia. Extensão ambiental do restaurante japonês aprovado, sem caricatura, comida, pessoa, palavra, logo ou UI.'
};
for(const a of p.assets){
 a.status='validado';a.visual_review=checks[a.id];a.qa={technical:true,visual:true,identity:!!a.character_id,identity_applicable:!!a.character_id};
 const asset=m.assets.find(x=>x.id===a.id);
 Object.assign(asset,{status:'validado',qa:{...a.qa,record:'docs/art/qa/ART-26.json'},visual_composition_proposal:checks[a.id]});
}
p.status='validado';p.qa_record='docs/art/qa/ART-26.json';
Object.assign(m.tasks.find(t=>t.id===tid),{status:'validado',qa_record:'docs/art/qa/ART-26.json'});
Object.assign(index.find(t=>t.id===tid),{status:'validado',qa_record:'docs/art/qa/ART-26.json'});
const qa={
 task_id:tid,title:'Retratos de equipe e ambientes de trabalho',date:p.date,status:'validado',reviewer:'Codex — inspeção visual e técnica',scope_resolution:p.scope_resolution,
 production:{tool:p.tool,model:null,model_note:p.model_note,distinct_asset_generations:10,revision_generations:1,prompts_record:'docs/art/prompts/ART-26.json',technical_export_source:p.producer_source},
 technical:{passed:true,asset_count:10,report:'docs/art/qa/ART-26-technical.json',plan_report:'docs/art/qa/ART-26-plan-validation.json',prerequisite_report:'docs/art/qa/ART-26-prerequisites.json',scope_report:'docs/art/qa/ART-26-scope-audit.json'},
 visual:{
  passed:true,backgrounds:['#F5F0E8','#24113D'],contact_sheet:p.delivery_preview,identity_comparison:p.identity_preview,layer_proof:p.layer_proof,
  inspected_previews:[p.delivery_preview,p.identity_preview,p.layer_proof,...p.assets.map(a=>a.native_preview)],
  checks:[
   'Dez arquivos individuais conferidos sobre fundos claro/escuro, nas dimensões de export e nos tamanhos de exibição 240×240/320×240.',
   'Cinco retratos comparados aos masters canônicos CH01, CH02, CH05, CH03 e CH06; preservados rostos, pele, olhos, penteados, figurinos e acessórios. Hashes canônicos conferidos e arquivos originais preservados.',
   'Cabeças, cabelos, ombros, cotovelos e mãos completos nos sprites. Cortes inferiores intencionais abaixo das mãos, com padding transparente, sem fades. Braços cruzados do chapeiro seguem o concept.',
   'Alfa real nos retratos, incluindo vãos e antialias; sem checkerboard, fundo incorporado, halo amplo ou sombra de chão. Corpos têm predominantemente alfa próximo de 255 conforme o ImageGen; transparência original preservada sem pintura/máscara/limpeza por código.',
   'Cinco ambientes 4:3 opacos full bleed, com materiais, contornos e luz compatíveis com os concepts. Sem pessoas, texto, valores, logos, UI ou gráficos incorporados. Pequenos utensílios/plantas posteriores são ambientação fixa; não foram criados props reutilizáveis separados fora do lote.',
   'Pessoa e ambiente são PNGs independentes. Prova de montagem usa escala uniforme e alinha o corte de meio corpo à base do viewport; não altera os sprites nem implementa UI.',
   'ImageGen produziu retratos RGBA em 1254×1254 e ambientes RGB em 1448×1086. Export técnico: retratos reduzidos uniformemente para 408×408 mais 52 px de padding por lado; ambientes reduzidos uniformemente para 960×720 e convertidos para RGBA com alfa 255. Nenhum crop, deformação x/y, recolor ou alteração manual de alfa.',
   'Uma revisão pelo ImageGen removeu planta de primeiro plano e cadeira do escritório. Geração inicial e revisão preservadas, com prompts e hashes separados.'
  ],
  display_semantics:'display_size_px é caixa de layout: retratos contain/preserveAspect; ambientes cover 4:3 com crop zero. Pivot [0.5,0.5], 9-slice [0,0,0,0].',
  layer_proof_transform:p.layer_proof_transform
 },
 assets:p.assets.map(a=>({id:a.id,name:a.short_name,output_path:a.output_path,actual_size_px:a.actual_size_px,display_size_px:a.display_size_px,sha256:a.sha256,output_bytes:a.output_bytes,source_path:a.source_path,source_sha256:a.source_sha256,source_size_px:a.source_size_px,references:a.references,reference_sha256:a.reference_sha256,canonical_identity:a.canonical_identity,visual_review:a.visual_review,measurements:a.measurements,native_preview:a.native_preview,display_preview:a.display_preview})),
 total_bytes:p.assets.reduce((n,a)=>n+a.output_bytes,0),
 integration_proposal:{textureType:'Sprite (2D and UI)',spriteMode:'Single',pixelsPerUnit:100,pivot:[0.5,0.5],nine_slice_lbrt_px:[0,0,0,0],maxTextureSize:'512 para retratos; 1024 para ambientes',sRGBTexture:true,alphaIsTransparency:true,filterMode:'Bilinear',generateMipmaps:false,compression:'Sem compressão durante QA; avaliar plataforma na integração.'},
 pending:[],integration_performed:false
};
write('docs/art/manifest.json',m);write('docs/art/tasks.json',index);write('docs/art/prompts/ART-26.json',p);write('docs/art/qa/ART-26.json',qa);
const changed=Object.entries(baseline.protected_files).filter(([f,h])=>!fs.existsSync(path.join(root,f))||sha(f)!==h).map(([f])=>f);
const audit={task_id:tid,other_asset_records_unchanged:equal(baseline.other_assets,m.assets.filter(a=>a.task_id!==tid)),other_task_records_unchanged:equal(baseline.other_tasks,m.tasks.filter(t=>t.id!==tid)),other_tasks_index_unchanged:equal(baseline.other_index,index.filter(t=>t.id!==tid)),manifest_other_fields_unchanged:equal(baseline.manifest_other_fields,Object.fromEntries(Object.entries(m).filter(([k])=>!['assets','tasks'].includes(k)))),protected_file_count:Object.keys(baseline.protected_files).length,protected_files_unchanged:changed.length===0,changed_protected_files:changed};
audit.passed=Object.entries(audit).filter(([k,v])=>typeof v==='boolean').every(([,v])=>v);write('docs/art/qa/ART-26-scope-audit.json',audit);
if(!audit.passed)throw Error('Scope audit failed: '+JSON.stringify(audit));
for(const [flag,dest] of [['--task','ART-26-technical'],['--plan','ART-26-plan-validation']]){
 const args=['tools/art/validate_assets.py','--project',root,flag,...(flag==='--task'?[tid]:[])],res=cp.spawnSync('python',args,{cwd:root,encoding:'utf8'});
 fs.writeFileSync(path.join(root,'docs/art/qa/'+dest+'.json'),res.stdout);if(res.status!==0)throw Error('Final validation failed '+res.stdout+res.stderr);
}
console.log(JSON.stringify({task:tid,status:'validado',asset_count:p.assets.length,total_bytes:qa.total_bytes,protected_file_count:audit.protected_file_count,scope_audit_passed:audit.passed,pending:[]}));

