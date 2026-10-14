/* Verify non-ART-09 records and all previously validated production PNGs. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const sha=d=>crypto.createHash('sha256').update(d).digest('hex');
const before=read('docs/art/qa/ART-09-scope-baseline.json'),m=read('docs/art/manifest.json');
const checks={
  other_asset_records_unchanged:before.other_asset_records_sha256===sha(JSON.stringify(m.assets.filter(a=>a.task_id!=='ART-09'))),
  other_task_records_unchanged:before.other_task_records_sha256===sha(JSON.stringify(m.tasks.filter(t=>t.id!=='ART-09'))),
  protected_files:Object.fromEntries(Object.entries(before.protected_files).map(([p,h])=>[p,sha(fs.readFileSync(path.join(root,p)))===h]))
};
const passed=checks.other_asset_records_unchanged&&checks.other_task_records_unchanged&&Object.values(checks.protected_files).every(Boolean);
fs.writeFileSync(path.join(root,'docs/art/qa/ART-09-scope-audit.json'),JSON.stringify({task_id:'ART-09',date:'2026-10-14',passed,...checks},null,2)+'\n');
console.log(JSON.stringify({task_id:'ART-09',passed,other_asset_records_unchanged:checks.other_asset_records_unchanged,
  other_task_records_unchanged:checks.other_task_records_unchanged,protected_file_count:Object.keys(checks.protected_files).length,
  protected_files_unchanged:Object.values(checks.protected_files).every(Boolean)}));
if(!passed)process.exitCode=1;
