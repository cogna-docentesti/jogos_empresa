/* ART-04: one editable SVG and one exact-size RGBA PNG per button state.
 * NODE_PATH must include the bundled sharp package. Existing assets are protected.
 */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../..');
const read = p => JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha = data => crypto.createHash('sha256').update(data).digest('hex');
const fileHash = p => sha(fs.readFileSync(path.join(root,p)));
const writeJSON = (p,data) => {
  fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true});
  fs.writeFileSync(path.join(root,p),JSON.stringify(data,null,2)+'\n','utf8');
};
const manifest = read('docs/art/manifest.json');
const tokens = read('docs/art/tokens.json');
const assets = manifest.assets.filter(a => a.task_id === 'ART-04');
const p = tokens.palette;
const mix = (a,b,t) => '#' + [1,3,5].map(i => Math.round(
  parseInt(a.slice(i,i+2),16)*(1-t)+parseInt(b.slice(i,i+2),16)*t
).toString(16).padStart(2,'0')).join('');
const families = {
  primary_violet: {
    label:'Primário violeta', base:p.violet,
    normal:{fill:p.violet,stroke:mix(p.violet,p.lavender,.45)},
    hover:{fill:mix(p.violet,'#ffffff',.12),stroke:p.lavender},
    pressed:{fill:mix(p.violet,p.plum,.24),stroke:mix(p.violet,p.plum,.5)},
    disabled:{fill:mix(p.violet,p.ivory,.72),stroke:mix(p.violet,p.ivory,.8)}
  },
  secondary_ivory: {
    label:'Secundário marfim', base:p.ivory,
    normal:{fill:p.ivory,stroke:mix(p.ivory,p.lavender,.7)},
    hover:{fill:mix(p.ivory,p.lavender,.18),stroke:mix(p.ivory,p.violet,.55)},
    pressed:{fill:mix(p.ivory,p.lavender,.42),stroke:mix(p.ivory,p.violet,.7)},
    disabled:{fill:mix(p.ivory,p.ink,.07),stroke:mix(p.ivory,p.ink,.13)}
  },
  danger_coral: {
    label:'Ação de risco coral', base:p.coral,
    normal:{fill:p.coral,stroke:mix(p.coral,p.plum,.12)},
    hover:{fill:mix(p.coral,'#ffffff',.12),stroke:mix(p.coral,p.ivory,.6)},
    pressed:{fill:mix(p.coral,p.plum,.22),stroke:mix(p.coral,p.plum,.42)},
    disabled:{fill:mix(p.coral,p.ivory,.72),stroke:mix(p.coral,p.ivory,.8)}
  },
  accent_amber: {
    label:'Destaque âmbar', base:p.amber,
    normal:{fill:p.amber,stroke:mix(p.amber,p.ivory,.4)},
    hover:{fill:mix(p.amber,'#ffffff',.22),stroke:mix(p.amber,p.ivory,.78)},
    pressed:{fill:mix(p.amber,p.ink,.2),stroke:mix(p.amber,p.ink,.38)},
    disabled:{fill:mix(p.amber,p.ivory,.72),stroke:mix(p.amber,p.ivory,.8)}
  },
  icon_button: {
    label:'Base compacta para ícone', base:mix(p.ivory,p.lavender,.2),
    normal:{fill:mix(p.ivory,p.lavender,.2),stroke:mix(p.ivory,p.lavender,.8)},
    hover:{fill:mix(p.ivory,p.lavender,.4),stroke:mix(p.ivory,p.violet,.6)},
    pressed:{fill:mix(p.ivory,p.lavender,.64),stroke:mix(p.ivory,p.violet,.75)},
    disabled:{fill:mix(p.ivory,p.ink,.07),stroke:mix(p.ivory,p.ink,.13)}
  }
};
const stateDescriptions = {
  normal:'Estado normal: cor base e contorno discreto, conforme a família.',
  hover:'Estado hover: realce de cor e contorno mais perceptível, sem halo, crescimento ou deslocamento.',
  pressed:'Estado pressionado: preenchimento mais profundo e contorno de maior peso visual; nenhuma translação da geometria.',
  disabled:'Estado desabilitado: preenchimento dessaturado de baixo contraste e contorno discreto; centro permanece opaco para leitura consistente nos fundos claros e escuros.'
};
const references = [
  'art-source/references/concepts/01-hub.png',
  'art-source/references/concepts/02-abertura.png',
  'art-source/references/concepts/03-gestao.png',
  'Assets/Art/new/shared/components/panels/panel_ivory.png',
  'Assets/Art/new/shared/components/panels/panel_lavender.png'
];
async function main() {
  if (assets.length !== 20) throw new Error('Unexpected ART-04 inventory');
  for (const a of assets) {
    if (a.production_method !== 'vector_or_code' || a.display_fit !== 'sliced') throw new Error(`Review production contract: ${a.id}`);
    for (const target of [`${manifest.asset_root}/${a.path}`,a.source_editable]) {
      if (fs.existsSync(path.join(root,target))) throw new Error(`Existing asset/source: ${target}`);
    }
  }
  for (const a of manifest.assets.filter(a => a.task_id === 'ART-03')) {
    if (a.status !== 'validado' || fileHash(`${manifest.asset_root}/${a.path}`) !== a.sha256) throw new Error(`Dependency changed: ${a.id}`);
  }
  writeJSON('docs/art/qa/ART-04-scope-baseline.json',{
    task_id:'ART-04',date:'2026-10-14',
    other_asset_records_sha256:sha(JSON.stringify(manifest.assets.filter(a => a.task_id !== 'ART-04'))),
    other_task_records_sha256:sha(JSON.stringify(manifest.tasks.filter(t => t.id !== 'ART-04'))),
    protected_files:Object.fromEntries([
      'docs/art/characters.json','docs/art/tokens.json','docs/art/catalogos.json',
      ...manifest.assets.filter(a => a.status === 'validado').map(a => `${manifest.asset_root}/${a.path}`)
    ].map(p => [p,fileHash(p)]))
  });
  const records=[];
  for (const a of assets) {
    const stem=path.basename(a.path,'.png');
    const state=stem.split('_').at(-1);
    const family=stem.slice(0,-state.length-1);
    const style=families[family]?.[state];
    if (!style) throw new Error(`Unknown family/state: ${stem}`);
    const [w,h]=a.export_size_px;
    const radius=12, border=2;
    // Opaque base under a single shared clip gives identical alpha in all states.
    // Flat fills avoid a stretched gradient or visible joins in nine-slice exports.
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><defs><clipPath id="silhouette"><rect width="${w}" height="${h}" rx="${radius}"/></clipPath></defs><g clip-path="url(#silhouette)"><rect width="${w}" height="${h}" fill="${style.fill}"/><rect x="1" y="1" width="${w-2}" height="${h-2}" rx="${radius-1}" fill="none" stroke="${style.stroke}" stroke-width="${border}"/></g></svg>\n`;
    const source=path.join(root,a.source_editable);
    const output=path.join(root,manifest.asset_root,a.path);
    fs.mkdirSync(path.dirname(source),{recursive:true});
    fs.mkdirSync(path.dirname(output),{recursive:true});
    fs.writeFileSync(source,svg,'utf8');
    await sharp(Buffer.from(svg),{density:72}).toColourspace('srgb').ensureAlpha()
      .withIccProfile('srgb').png({palette:false,compressionLevel:9}).toFile(output);
    const metadata=await sharp(output).metadata();
    if (metadata.width!==w || metadata.height!==h || metadata.channels!==4) throw new Error(`Invalid PNG: ${a.id}`);
    const prompt=[
      `Produzir somente a base do botão ${family}, estado ${state}, no estilo UI aprovado Cogna.`,
      stateDescriptions[state],
      `Preenchimento uniforme ${style.fill}; contorno interno opaco ${style.stroke} de 2 px; raio externo 12 px.`,
      `PNG RGBA 8-bit sRGB ${w}×${h} px; display ${a.display_size_px.join('×')} px em 9-slice com bordas l,b,r,t [${a.nine_slice_lbrt_px.join(', ')}].`,
      'Mesma silhueta alfa, bounds e pivot central [0.5, 0.5] nos quatro estados; cantos transparentes antialias, centro opaco.',
      'Sem texto, label, ícone, números, logo, conteúdo, sombra, halo, cenário ou quadriculado incorporados.',
      'Concepts determinam a linguagem do estado normal; estados de interação são derivados dos tokens, com coerência entre famílias e sem alteração da geometria.'
    ].join('\n');
    records.push({
      id:a.id,family,state,output_path:`${manifest.asset_root}/${a.path}`,
      source_editable:a.source_editable,source_sha256:fileHash(a.source_editable),
      origin:'Geometria SVG determinística; paleta e medidas do manifesto/tokens, concepts aprovados e superfícies ART-03.',
      references:references.map(p => ({path:p,sha256:fileHash(p)})),
      final_prompt:prompt,tool:'deterministic SVG + sharp/librsvg',model:null,
      actual_size_px:[w,h],mode:'RGBA',color_space:'sRGB',sha256:fileHash(`${manifest.asset_root}/${a.path}`),
      geometry:{outer_radius_px:radius,border_px:border,pivot:a.pivot,nine_slice_lbrt_px:a.nine_slice_lbrt_px},
      colors:style,status:'gerado'
    });
    Object.assign(a,{
      status:'gerado',source:'SVG editável; rasterização determinística sharp/librsvg',
      references:[...references],sha256:records.at(-1).sha256,actual_size_px:[w,h],
      generation_prompt:prompt,generation_tool:'deterministic SVG + sharp/librsvg',
      generation_model:null,generation_model_note:'Lote geométrico; nenhum modelo generativo necessário.',
      production_record:'docs/art/prompts/ART-04.json'
    });
  }
  writeJSON('docs/art/prompts/ART-04.json',{
    task_id:'ART-04',date:'2026-10-14',status:'gerado',
    scope:'Somente vinte bases de botão em cinco famílias e quatro estados, sem texto nem ícones.',
    tool:'SVG editável e rasterização determinística sharp/librsvg',model:null,
    model_note:'Nenhum modelo generativo necessário para geometria exata e estados consistentes.',
    producer_source:'art-source/editable/ART-04/build-art04.cjs',renderer_versions:sharp.versions,
    palette:p,state_formulas:families,assets:records
  });
  manifest.tasks.find(t => t.id==='ART-04').status='gerado';
  writeJSON('docs/art/manifest.json',manifest);
  const tasks=read('docs/art/tasks.json');
  tasks.find(t => t.id==='ART-04').status='gerado';
  writeJSON('docs/art/tasks.json',tasks);
  console.log(JSON.stringify({task_id:'ART-04',status:'gerado',png_count:records.length,svg_count:records.length}));
}
main().catch(e => {console.error(e);process.exitCode=1;});
