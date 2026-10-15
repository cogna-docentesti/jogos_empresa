"""Persist the actual manual ART-39 visual review, not an automated visual test.
The agent inspected the approved concept, six existing identities, raw master
light/dark diagnostic, both final native light/dark exports, both display boards,
the identity comparison and native face/hand details before running this script.
"""
from pathlib import Path
import json
import re
import sys

sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from produce_art39 import (ROOT,TASK,BASE,PROMPTS,PREVIEW,MASTER,IDS,BACKGROUNDS,
                           read,write,sha,git,validate)

QA_PATH = 'docs/art/qa/ART-39.json'
APPEARANCE = (
    'Homem adulto negro de cerca de 30 anos, pele castanha quente, olhos castanho-escuros, '
    'rosto oval retangular alongado com queixo suave, nariz largo arredondado, labios naturais '
    'e sobrancelhas escuras espessas. Cabelo preto crespo curto, cachos densos no topo e '
    'laterais discretamente aparadas. Sem barba, bigode, oculos, bone ou joias. Compleicao '
    'adulta esguia. Colete ameixa ajustado de decote V, tres botoes pequenos grafite e dois '
    'bolsos inferiores embutidos; camisa marfim com gola aberta sem gravata e mangas dobradas '
    'ate os antebracos; calca reta grafite e sapatos de trabalho fechados grafite. '
    'Sorriso leve de boca fechada. Preservar rosto, cabelo, pele, proporcoes e figurino.'
)
OBSERVATIONS = {
    'CH07_CANONICAL': (
        'Nova identidade adulta distinta de CH01-CH06: homem de cabelo crespo curto, sem barba, '
        'colete ameixa de tres botoes, camisa marfim, calca e sapatos grafite. Corpo inteiro '
        'em tres quartos, bracos relaxados, duas maos coerentes e dois sapatos completos. '
        'Rosto, olhos, cabelo e silhueta legiveis. Recorte sem checkerboard ou halo visivel '
        'sobre marfim/ameixa, sem cenario, props, texto ou UI. Caixa 420x760 usa contain '
        'com raster 420x630 e offset 0,65; proporcoes preservadas.'
    ),
    'PORTRAIT_WAITER': (
        'O retrato conserva rosto, tom de pele, olhos, nariz, labios, cachos curtos, ausencia '
        'de barba e colete ameixa de tres botoes do master CH07 real. Pose de meio corpo '
        'com maos relaxadas sobrepostas a cintura; dedos, ombros, cotovelos e cabelo completos. '
        'Corte inferior intencional abaixo das maos e margem transparente. Leitura clara '
        'a 240x240, sem deformacao; alfa limpo visualmente em marfim e ameixa, sem halo '
        'visivel, fundo, texto, salario, props ou UI.'
    ),
}


def scope_audit():
    m = read('docs/art/manifest.json')
    old = json.loads(git('show',BASE+':docs/art/manifest.json'))
    changes = {}
    for key in ['assets','tasks']:
        before = {x['id']:x for x in old[key]}
        after = {x['id']:x for x in m[key]}
        assert before.keys()==after.keys()
        changes[key]=[k for k in before if before[k]!=after[k]]
    assert changes=={'assets':IDS,'tasks':[TASK]},changes
    assert m['characters'][:-1]==old['characters']
    assert m['characters'][-1]['id']=='CH07'
    assert {k:v for k,v in old.items() if k not in ['assets','tasks','characters']}=={
        k:v for k,v in m.items() if k not in ['assets','tasks','characters']}
    old_chars=json.loads(git('show',BASE+':docs/art/characters.json'))
    assert read('docs/art/characters.json')[:-1]==old_chars
    old_tasks=json.loads(git('show',BASE+':docs/art/tasks.json'))
    assert [x for x in read('docs/art/tasks.json') if x['id']!=TASK]==[x for x in old_tasks if x['id']!=TASK]
    prior=read('docs/art/qa/ART-39-prerequisites.json')['baseline_assets']
    for a in prior: assert sha(a['path'])==a['sha256'],a['id']
    paths=set(git('diff','--name-only',BASE).splitlines())
    paths.update(git('ls-files','--others','--exclude-standard').splitlines())
    paths.update([QA_PATH,'docs/art/qa/ART-39-scope-audit.json'])
    finals={a['path'] for a in read(PROMPTS)['assets']}
    exact=finals|{p+'.meta' for p in finals}|{
        'docs/art/manifest.json','docs/art/tasks.json','docs/art/characters.json',PROMPTS}
    prefixes=('art-source/editable/ART-39/','art-source/previews/ART-39/','docs/art/qa/ART-39')
    unexpected=sorted(p for p in paths if p not in exact and not p.startswith(prefixes))
    assert not unexpected,unexpected
    result={
        'task_id':TASK,'passed':True,'base_commit':BASE,'branch':git('branch','--show-current'),
        'initial_head_verified':True,'initial_tree_clean':True,'current_lot_only':True,
        'changed_manifest_asset_ids':changes['assets'],'changed_manifest_task_ids':changes['tasks'],
        'added_character_ids':['CH07'],'prior_six_character_records_preserved':True,
        'unrelated_manifest_records_preserved':True,'prior_png_hash_count':len(prior),
        'prior_png_hashes_preserved':True,'unrelated_changed_paths':unexpected,
        'reviewed_paths':sorted(paths),'recorded_before_commit':True,
    }
    write('docs/art/qa/ART-39-scope-audit.json',result)
    return result


def main():
    assert git('rev-parse','HEAD')==BASE
    p=read(PROMPTS)
    assert [a['id'] for a in p['assets']]==IDS
    previews=read('docs/art/qa/ART-39-preview-index.json')['previews']
    for path in previews: assert (ROOT/path).is_file()
    for a in p['assets']:
        assert a['sha256']==sha(a['path'])
        assert a['source_sha256']==sha(a['source_path'])
        assert a['canonical_sha256']==sha(MASTER)
        for path,digest in a['reference_sha256'].items(): assert sha(path)==digest
        a.update(status='validado',qa={'technical':True,'visual':True,'identity':True},
                 visual_observation=OBSERVATIONS[a['id']],inspected_previews=previews,
                 identity_comparison=PREVIEW+'/CH07-identity-comparison.png',
                 display_proof=PREVIEW+'/'+a['id']+'-display-light-dark.png')
        meta=(ROOT/(a['path']+'.meta')).read_text(encoding='utf-8')
        for expected in ['textureType: 8','spriteMode: 1','spriteMeshType: 0',
                         'spritePixelsToUnits: 100','spritePivot: {x: 0.5, y: 0.5}',
                         'spriteBorder: {x: 0, y: 0, z: 0, w: 0}','enableMipMap: 0',
                         'sRGBTexture: 1','alphaIsTransparency: 1','filterMode: 1',
                         'maxTextureSize: 2048','textureCompression: 0']:
            assert expected in meta,expected
    write(PROMPTS,p)
    character={
        'id':'CH07','name':'Garçom — extensão de arte',
        'origin':'Especificação de produção ART-39 explicitamente autorizada; estilo de 03-gestao.png e masters existentes.',
        'roles':['waiter'],'appearance':APPEARANCE,
        'canonical_asset':'characters/CH07/ch07_canonical.png','status':'validado',
        'identity_hash':sha(MASTER),
        'allowed_changes':['pose','expressão natural','objeto segurado previsto'],
        'locked_features':['rosto','tom de pele','cor dos olhos','penteado','figurino base',
                           'acessórios de identidade','proporção corporal'],
        'canonical_reference_path':MASTER,'actual_size_px':[1024,1536],
        'identity_qa_record':QA_PATH,'source_raster':p['assets'][0]['source_path'],
        'source_raster_sha256':p['assets'][0]['source_sha256'],
        'scope_note':'Art identity only. No RoleData, enum, gameplay, role catalog or balance change.',
        'dependent_portrait':'Assets/Art/new/team/portraits/waiter.png',
    }
    chars=read('docs/art/characters.json')
    assert [x['id'] for x in chars]==['CH01','CH02','CH03','CH04','CH05','CH06']
    chars.append(character)
    write('docs/art/characters.json',chars)
    m=read('docs/art/manifest.json')
    assert m['characters']==chars[:-1]
    m['characters'].append(character)
    for a in m['assets']:
        if a['id'] in IDS:
            a.update(status='validado',qa={'technical':True,'visual':True,'identity':True,
                     'record':QA_PATH,'notes':OBSERVATIONS[a['id']],
                     'preview':PREVIEW+'/'+a['id']+'-display-light-dark.png',
                     'identity_comparison':PREVIEW+'/CH07-identity-comparison.png'},
                     canonical_asset=MASTER,canonical_sha256=sha(MASTER))
    task=next(t for t in m['tasks'] if t['id']==TASK)
    task.update(status='validado',qa_record=QA_PATH,
                authorization='Explicitly authorized ART-39 CH07 art extension; no game role or catalog additions.')
    write('docs/art/manifest.json',m)
    tasks=read('docs/art/tasks.json')
    task2=next(t for t in tasks if t['id']==TASK)
    task2.update(status='validado',qa_record=QA_PATH,authorization=task['authorization'])
    write('docs/art/tasks.json',tasks)
    validate()
    scope=scope_audit()
    record={
        'task_id':TASK,'status':'validado','date':'2026-10-15','asset_count':2,'new_sprite_count':2,
        'technical':{
            'passed':True,'task_validation':'docs/art/qa/ART-39-technical.json',
            'plan_validation':'docs/art/qa/ART-39-plan-validation.json',
            'prerequisites':'docs/art/qa/ART-39-prerequisites.json',
            'prerequisite_asset_count':16,'prior_png_hash_count':270,
            'rgba_srgb':True,'true_transparency':True,
            'transparent_margins_at_least_10_percent':True,
            'source_rasters_preserved':True,
            'import_metadata_verified':True,'unity_executed':False,
            'import_verification':'Serialized Single/FullRect, Bilinear, PPU100, no mipmaps, alpha transparency, sRGB, max2048 and uncompressed platform settings inspected.',
            'source_size_note':'Canonical generation is 1024x1536. Portrait generation returned 1254x1254 despite desired 1024x1024; actual dimensions recorded, uniformly reduced and padded to exact 512x512.',
        },
        'visual':{
            'passed':True,'identity_passed':True,
            'method':'Actual manual inspection of the listed references and proofs.',
            'references_inspected':['art-source/references/concepts/03-gestao.png',
                                    'Assets/Art/new/characters/CH06/ch06_canonical.png',
                                    'Assets/Art/new/team/portraits/manager.png',
                                    PREVIEW+'/existing-six-identities.png',MASTER],
            'backgrounds':BACKGROUNDS,'native_sizes_inspected_px':[[1024,1536],[512,512]],
            'display_boxes_inspected_px':[[420,760],[240,240]],
            'display_fit':'contain','inspected_previews':previews,
            'raw_diagnostics_inspected':[PREVIEW+'/CH07_CANONICAL-initial-light-dark.png'],
            'canonical_master':MASTER,'canonical_master_sha256':sha(MASTER),
            'portrait_uses_real_master':True,
            'observations':OBSERVATIONS,'final_ui_proposed':False,
        },
        'character':character,'assets':p['assets'],'production_record':PROMPTS,'scope':scope,
        'pending':[],
        'integration_pending':[
            'Future Unity integration remains outside ART-39.',
            'CH07 is art only; waiter has no RoleData. No game role, catalog or balance was added.',
            'ART-36 historical audit reported 227 earlier importers differing from proposals and no matches for its 265 GUIDs in 175 serialized files; those findings were not re-audited or repaired in ART-39.',
            'BMC, documentary reports and debriefing still await their final model/screen.',
        ],
        'commit_reference':'Recorded by queue finish after creation of the single commit.',
        'integration_note':'No Unity execution, scenes, gameplay, catalogs, economy, packages or push.',
    }
    write(QA_PATH,record)
    print('Manual visual/identity QA recorded; exact two-asset scope and prior 270 PNG hashes preserved.')


if __name__=='__main__':
    main()
