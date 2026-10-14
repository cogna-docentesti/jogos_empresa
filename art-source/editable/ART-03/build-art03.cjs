/* ART-03: editable SVG sources and deterministic, exact-size RGBA exports.
 * Run from the project root with the bundled sharp package on NODE_PATH.
 * Existing production PNGs are never overwritten by this initial producer.
 */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'docs/art/manifest.json'), 'utf8'));
const tokens = JSON.parse(fs.readFileSync(path.join(root, 'docs/art/tokens.json'), 'utf8'));
const assets = manifest.assets.filter(a => a.task_id === 'ART-03');
const palette = tokens.palette;
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
const fileHash = p => sha(fs.readFileSync(path.join(root, p)));
const writeJSON = (p, data) => {
  const target = path.join(root, p);
  fs.mkdirSync(path.dirname(target), {recursive: true});
  fs.writeFileSync(target, JSON.stringify(data, null, 2) + '\n', 'utf8');
};
const mix = (a, b, t) => '#' + [1, 3, 5].map(i => Math.round(
  parseInt(a.slice(i, i + 2), 16) * (1 - t) + parseInt(b.slice(i, i + 2), 16) * t
).toString(16).padStart(2, '0')).join('');
const rounded = (w, h, fill, stroke, strokeOpacity = 1, strokeWidth = 1, radius = 16, opacity = 1) => {
  const inset = strokeWidth / 2;
  return `<rect x="${inset}" y="${inset}" width="${w-strokeWidth}" height="${h-strokeWidth}" rx="${radius-inset}" fill="${fill}" fill-opacity="${opacity}" stroke="${stroke}" stroke-opacity="${strokeOpacity}" stroke-width="${strokeWidth}"/>`;
};
const common = 'Componente isolado, sem texto, ícone, conteúdo de card ou sombra incorporada. PNG RGBA 8-bit em sRGB; bordas antialias com alfa verdadeiro. Pivot central. Cantos e bordas contidos nas faixas 9-slice do manifesto.';
const designs = {
  UI_TOPBAR_VIOLET: {
    body: `<defs><linearGradient id="violet" x1="0" y1="0" x2="1" y2="0"><stop stop-color="${mix(palette.violet, '#ffffff', .06)}"/><stop offset=".5" stop-color="${palette.violet}"/><stop offset="1" stop-color="${mix(palette.violet, palette.plum, .08)}"/></linearGradient></defs><rect width="1920" height="88" fill="url(#violet)"/>`,
    spec: 'Barra superior opaca de 1920×88. Gradiente horizontal discreto em torno do violeta #6D28D9, conforme o concept 01-hub. Sem linha, sombra ou arredondamento.'
  },
  UI_NAVBAR_PLUM: {
    body: `<rect width="1920" height="96" fill="${palette.plum}"/><rect width="1920" height="1" fill="${palette.lavender}" fill-opacity=".2"/>`,
    spec: 'Barra inferior opaca de 1920×96 em ameixa #24113D; contorno superior lavanda de 1 px a 20% de opacidade. Sem sombra ou arredondamento.'
  },
  UI_PANEL_IVORY: {
    body: rounded(256, 256, palette.ivory, palette.ink, .12),
    spec: 'Painel marfim #F5F0E8. Raio externo 16 px; contorno de 1 px em #30243C a 12%. Centro opaco e uniforme; cantos transparentes; bordas 24 px.'
  },
  UI_PANEL_LAVENDER: {
    body: rounded(256, 256, mix(palette.ivory, palette.lavender, .2), palette.lavender, .5),
    spec: `Painel lavanda suave ${mix(palette.ivory, palette.lavender, .2)} derivado de 80% marfim e 20% lavanda dos tokens, conforme superfícies claras dos concepts. Raio externo 16 px; contorno lavanda de 1 px a 50%. Centro opaco e uniforme; bordas 24 px.`
  },
  UI_PANEL_PLUM: {
    body: rounded(256, 256, palette.plum, palette.lavender, .3),
    spec: 'Painel ameixa #24113D. Raio externo 16 px; contorno lavanda de 1 px a 30%. Centro opaco e uniforme; cantos transparentes; bordas 24 px.'
  },
  UI_PANEL_SHADOW: {
    body: `<defs><filter id="soft" x="0" y="0" width="256" height="256" filterUnits="userSpaceOnUse"><feGaussianBlur stdDeviation="2.5"/></filter></defs><rect x="8" y="8" width="240" height="240" rx="14" fill="${palette.plum}" fill-opacity=".18" filter="url(#soft)"/>`,
    spec: 'Sombra independente, sem superfície. Ameixa a 18%, retângulo interno em (8,8) de 240×240, raio 14 px e desfoque gaussiano sigma 2,5 px. Margem transparente e cantos contidos nas faixas 32 px. Alinhar centralizada atrás do painel: caixa 580×860 versus painel 568×848.'
  },
  UI_CARD_NORMAL: {
    body: rounded(256, 256, mix(palette.ivory, '#ffffff', .55), palette.ink, .12),
    spec: `Card claro ${mix(palette.ivory, '#ffffff', .55)} derivado do marfim, como os cards do concept 03-gestao. Raio externo 16 px; contorno de 1 px em #30243C a 12%. Canvas e bounds iguais aos estados selecionado/desabilitado; bordas 24 px.`
  },
  UI_CARD_SELECTED_OUTLINE: {
    body: rounded(256, 256, 'none', palette.violet, 1, 3),
    spec: 'Apenas contorno violeta #6D28D9 de 3 px, sem preenchimento; centro com alfa zero. Raio externo 16 px e bounds externos iguais ao card normal. Bordas 24 px; aplicar como camada sobre o card normal.'
  },
  UI_CARD_DISABLED_OVERLAY: {
    body: `<rect width="256" height="256" rx="16" fill="${palette.ivory}" fill-opacity=".64"/>`,
    spec: 'Overlay marfim #F5F0E8 a 64% de opacidade, centro semitransparente. Raio externo 16 px, canvas e bounds iguais ao card normal. Bordas 24 px; aplicar sobre conteúdo e superfície do card.'
  },
  UI_MODAL_SURFACE: {
    body: rounded(256, 256, palette.ivory, palette.lavender, .5),
    spec: 'Superfície modal marfim #F5F0E8. Raio externo 16 px; contorno lavanda de 1 px a 50%; centro opaco e uniforme. Sem scrim ou sombra incorporados; bordas 24 px.'
  },
  UI_TOOLTIP_SURFACE: {
    body: rounded(256, 128, palette.ivory, palette.ink, .18, 1, 12),
    spec: 'Superfície de tooltip 256×128, marfim #F5F0E8. Raio externo 12 px; contorno #30243C de 1 px a 18%; centro opaco uniforme. Sem seta ou texto; bordas 20 px.'
  },
  UI_SCRIM: {
    body: `<rect width="1" height="1" fill="${palette.plum}" fill-opacity=".56"/>`,
    spec: 'Scrim sólido de 1×1 px em #24113D a 56% de opacidade. Preenche a caixa inteira de 1920×1080 por expansão do pixel uniforme, sem preservar proporção de imagem. Nenhum gradiente.'
  },
  UI_BADGE_CIRCLE: {
    body: `<circle cx="64" cy="64" r="62" fill="${palette.violet}" stroke="${palette.lavender}" stroke-width="2"/>`,
    spec: 'Base circular 128×128, centro (64,64), raio do traço 62 px, fill violeta #6D28D9 e contorno lavanda de 2 px. Sem ícone. Display 64×64 com escala uniforme e pivot central.'
  }
};

async function main() {
  // Guard all final paths before producing anything, including the copied logo.
  for (const a of assets) {
    if (fs.existsSync(path.join(root, manifest.asset_root, a.path))) {
      throw new Error(`Production asset already exists: ${a.path}`);
    }
    if (a.source_editable && fs.existsSync(path.join(root, a.source_editable))) {
      throw new Error(`Editable source already exists: ${a.source_editable}`);
    }
  }
  writeJSON('docs/art/qa/ART-03-scope-baseline.json', {
    task_id: 'ART-03', date: '2026-10-14',
    other_asset_records_sha256: sha(JSON.stringify(manifest.assets.filter(a => a.task_id !== 'ART-03'))),
    other_task_records_sha256: sha(JSON.stringify(manifest.tasks.filter(t => t.id !== 'ART-03'))),
    protected_files: Object.fromEntries([
      'docs/art/characters.json', 'docs/art/tokens.json', 'docs/art/catalogos.json',
      ...manifest.assets.filter(a => /^CH0[1-6]_CANONICAL$/.test(a.id)).map(a => `${manifest.asset_root}/${a.path}`)
    ].map(p => [p, fileHash(p)]))
  });
  const records = [];
  for (const a of assets.filter(a => a.production_method === 'vector_or_code')) {
    const d = designs[a.id];
    if (!d) throw new Error(`No geometry for ${a.id}`);
    const [w,h] = a.export_size_px;
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${d.body}</svg>\n`;
    const source = path.join(root, a.source_editable);
    const output = path.join(root, manifest.asset_root, a.path);
    fs.mkdirSync(path.dirname(source), {recursive: true});
    fs.mkdirSync(path.dirname(output), {recursive: true});
    fs.writeFileSync(source, svg, 'utf8');
    await sharp(Buffer.from(svg), {density: 72}).toColourspace('srgb').ensureAlpha()
      .withIccProfile('srgb').png({palette: false, compressionLevel: 9}).toFile(output);
    const metadata = await sharp(output).metadata();
    if (metadata.width !== w || metadata.height !== h || metadata.channels !== 4) throw new Error(`Invalid export: ${a.id}`);
    const prompt = `${d.spec}\n${common}\nExport ${w}×${h} px; display ${a.display_size_px.join('×')} px; 9-slice l,b,r,t [${a.nine_slice_lbrt_px.join(', ')}].`;
    records.push({
      id: a.id, output_path: `${manifest.asset_root}/${a.path}`, source_editable: a.source_editable,
      source_sha256: fileHash(a.source_editable), sha256: fileHash(`${manifest.asset_root}/${a.path}`),
      references: a.references.map(p => ({path: p, sha256: fileHash(p)})),
      final_prompt: prompt, tool: 'deterministic SVG + sharp/librsvg', model: null,
      actual_size_px: [metadata.width, metadata.height], mode: 'RGBA', color_space: 'sRGB',
      nine_slice_lbrt_px: a.nine_slice_lbrt_px, pivot: a.pivot, status: 'gerado'
    });
  }
  writeJSON('docs/art/prompts/ART-03.json', {
    task_id: 'ART-03', date: '2026-10-14', status: 'gerado',
    scope: 'Somente os 14 assets de barras, painéis e bases compartilhadas; logo oficial reutilizado. Personagens já entregues não são gerados novamente.',
    tool: 'SVG editável e rasterização determinística sharp/librsvg; Pillow para conversão do logo paletizado',
    model: null, model_note: 'Nenhum modelo generativo necessário para este lote geométrico.',
    producer_source: 'art-source/editable/ART-03/build-art03.cjs', renderer_versions: sharp.versions,
    assets: records
  });
  console.log(JSON.stringify({task_id: 'ART-03', generated_vectors: records.length, logo_pending_conversion: true}));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
