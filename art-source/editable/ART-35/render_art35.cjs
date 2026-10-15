/* Exact SVG-to-PNG export. Set NODE_PATH to the installed sharp package directory. */
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../..');
async function main() {
  const m = JSON.parse(fs.readFileSync(path.join(root, 'docs/art/manifest.json'), 'utf8'));
  const assets = m.assets.filter(a => a.task_id === 'ART-35');
  if (assets.length !== 4) throw new Error('Unexpected ART-35 inventory');
  for (const a of assets) {
    if (a.production_method !== 'vector_or_code' || a.status !== 'planejado') throw new Error('Unexpected state: ' + a.id);
    const output = path.join(root, m.asset_root, a.path);
    if (fs.existsSync(output)) throw new Error('Refusing to overwrite: ' + output);
    fs.mkdirSync(path.dirname(output), {recursive: true});
    await sharp(path.join(root, a.source_editable), {density: 72})
      .toColourspace('srgb').ensureAlpha().withIccProfile('srgb')
      .png({palette: false, compressionLevel: 9}).toFile(output);
    const info = await sharp(output).metadata();
    if (info.width !== a.export_size_px[0] || info.height !== a.export_size_px[1] || info.channels !== 4) throw new Error('Wrong export: ' + a.id);
  }
  console.log(JSON.stringify({tool: 'sharp/librsvg', versions: sharp.versions, exported: 4}));
}
main().catch(e => {console.error(e); process.exitCode = 1;});
