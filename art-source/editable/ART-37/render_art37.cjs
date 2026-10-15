/* ART-37: four original, separate white BMC glyphs. No UI implementation. */
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../../..');
const stroke = body => `<g fill="none" stroke="#FFFFFF" stroke-width="16" stroke-linecap="round" stroke-linejoin="round">${body}</g>`;
const designs = {
  ICON_PARTNERS: stroke('<path d="M40 112 L64 72 L88 88 L64 136 Z M216 112 L192 72 L168 88 L192 136 Z M84 96 L104 80 H132 M172 96 L152 80 H132 L108 104 Q100 112 108 120 Q116 128 124 120 L136 108 L172 144 M68 136 L116 184 Q128 196 140 184 L188 136 M112 160 L132 180"/>'),
  ICON_VALUE_PROPOSITION: stroke('<path d="M72 56 H184 L216 104 L128 208 L40 104 Z M40 104 H216 M72 56 L96 104 L128 208 L160 104 L184 56"/>'),
  ICON_CUSTOMER_RELATIONSHIP: stroke('<path d="M72 184 H64 Q40 184 40 160 V72 Q40 48 64 48 H192 Q216 48 216 72 V160 Q216 184 192 184 H112 L72 216 Z"/>') + '<path fill="#FFFFFF" d="M128 148 L94 116 C70 92 104 68 128 92 C152 68 186 92 162 116 Z"/>',
  ICON_SALES_CHANNELS: stroke('<rect x="96" y="40" width="64" height="48" rx="8"/><path d="M128 88 V128 M64 168 V128 H192 V168 M128 128 V168"/><rect x="44" y="176" width="40" height="40" rx="8"/><rect x="108" y="176" width="40" height="40" rx="8"/><rect x="172" y="176" width="40" height="40" rx="8"/>'),
};
async function main() {
  const m = JSON.parse(fs.readFileSync(path.join(root, 'docs/art/manifest.json'), 'utf8'));
  const assets = m.assets.filter(a => a.task_id === 'ART-37');
  if (assets.length !== 4) throw new Error('Unexpected inventory');
  const revise = process.argv.includes('--revise');
  // Check every destination before writing anything; never overwrite a validated source.
  for (const a of assets) {
    if (a.status !== (revise ? 'gerado' : 'planejado') || a.production_method !== 'vector_or_code') throw new Error('Unexpected state: ' + a.id);
    for (const dest of [a.source_editable, m.asset_root + '/' + a.path]) {
      if (!revise && fs.existsSync(path.join(root, dest))) throw new Error('Refusing to overwrite: ' + dest);
    }
    if (revise && crypto.createHash('sha256').update(fs.readFileSync(path.join(root, m.asset_root, a.path))).digest('hex') !== a.sha256) throw new Error('Unrecorded change: ' + a.id);
  }
  for (const a of assets) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">${designs[a.id]}</svg>\n`;
    const source = path.join(root, a.source_editable);
    const output = path.join(root, m.asset_root, a.path);
    fs.mkdirSync(path.dirname(source), {recursive: true});
    fs.mkdirSync(path.dirname(output), {recursive: true});
    fs.writeFileSync(source, svg, 'utf8');
    await sharp(Buffer.from(svg), {density: 72}).toColourspace('srgb').ensureAlpha().withIccProfile('srgb')
      .png({palette: false, compressionLevel: 9}).toFile(output);
    const info = await sharp(output).metadata();
    if (info.width !== 256 || info.height !== 256 || info.channels !== 4) throw new Error('Wrong export: ' + a.id);
  }
  const info = {tool: 'sharp/librsvg', versions: sharp.versions, exported: 4};
  fs.writeFileSync(path.join(__dirname, 'renderer-info.json'), JSON.stringify(info, null, 2) + '\n');
  console.log(JSON.stringify(info));
}
main().catch(e => {console.error(e); process.exitCode = 1;});
