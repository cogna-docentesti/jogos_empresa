/* Verify task boundaries and protected character PNGs after production. */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../../..');
const read = p => JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha = data => crypto.createHash('sha256').update(data).digest('hex');
const before = read('docs/art/qa/ART-03-scope-baseline.json');
const manifest = read('docs/art/manifest.json');
const checks = {
  other_asset_records_unchanged: before.other_asset_records_sha256 === sha(JSON.stringify(manifest.assets.filter(a => a.task_id !== 'ART-03'))),
  other_task_records_unchanged: before.other_task_records_sha256 === sha(JSON.stringify(manifest.tasks.filter(t => t.id !== 'ART-03'))),
  protected_files: Object.fromEntries(Object.entries(before.protected_files).map(([p,h]) => [p,sha(fs.readFileSync(path.join(root,p))) === h]))
};
const passed = checks.other_asset_records_unchanged && checks.other_task_records_unchanged && Object.values(checks.protected_files).every(Boolean);
const report = {task_id:'ART-03',date:'2026-10-14',passed,...checks};
fs.writeFileSync(path.join(root,'docs/art/qa/ART-03-scope-audit.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report));
if (!passed) process.exitCode = 1;
