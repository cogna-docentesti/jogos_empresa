/* Technical export only: preserve generated art and alpha; fit uniformly without cropping. */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../..');
const input = process.argv[2];
const output = path.join(root, 'Assets/Art/new/location/map/map_base.png');
const hash = data => crypto.createHash('sha256').update(data).digest('hex');

async function main() {
  if (!input || !fs.existsSync(input)) throw new Error('Pass the selected integrated ImageGen output.');
  if (fs.existsSync(output)) throw new Error('Existing production map: do not overwrite.');
  const original = await sharp(input).metadata();
  if (original.channels !== 4) throw new Error('Input has no RGBA alpha: fix transparency using ImageGen.');
  const scale = Math.min(1920 / original.width, 1080 / original.height);
  fs.mkdirSync(path.dirname(output), { recursive: true });
  await sharp(input)
    .resize({ width: 1920, height: 1080, fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 }, kernel: 'lanczos3' })
    .toColourspace('srgb').ensureAlpha().withIccProfile('srgb')
    .png({ palette: false, compressionLevel: 9 }).toFile(output);
  const final = await sharp(output).metadata();
  if (final.width !== 1920 || final.height !== 1080 || final.channels !== 4 || final.depth !== 'uchar')
    throw new Error('Invalid export.');
  const record = {
    task_id: 'ART-10', input_path: input.replaceAll('\\', '/'),
    input_sha256: hash(fs.readFileSync(input)), input_size_px: [original.width, original.height],
    output_path: 'Assets/Art/new/location/map/map_base.png', output_sha256: hash(fs.readFileSync(output)),
    output_size_px: [final.width, final.height], mode: 'RGBA', color_space: 'sRGB', depth: '8-bit',
    operation: 'Technical uniform contain export only; no painting, masking, background removal or nonuniform stretch.',
    uniform_scale: scale,
    nominal_padding_px: [(1920 - original.width * scale) / 2, (1080 - original.height * scale) / 2],
    raster_rounding_note: 'Sharp rounds scaled dimensions and pads the remaining pixel. The final master is the coordinate authority.',
    alpha_preserved: true, art_created_by: 'image_gen.imagegen', renderer_versions: sharp.versions
  };
  const target = path.join(root, 'docs/art/qa/ART-10-export.json');
  fs.writeFileSync(target, JSON.stringify(record, null, 2) + '\n', 'utf8');
  console.log(JSON.stringify({ task: 'ART-10', input_size_px: record.input_size_px, output_size_px: record.output_size_px, sha256: record.output_sha256 }));
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
