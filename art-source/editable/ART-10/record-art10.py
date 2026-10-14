"""Record ART-10 generated assets, then finalize only after technical and visual QA."""
from pathlib import Path
import argparse, hashlib, json
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
TASK = 'ART-10'
def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))
def write(path, value):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def generated():
    manifest = read('docs/art/manifest.json')
    asset = next(a for a in manifest['assets'] if a['task_id'] == TASK)
    assert asset['id'] == 'MAP_BASE' and asset['status'] == 'planejado'
    final = ROOT / manifest['asset_root'] / asset['path']
    with Image.open(final) as image:
        assert image.size == (1920, 1080) and image.mode == 'RGBA'
        assert image.getchannel('A').getextrema() == (0, 255)
        assert image.info.get('icc_profile')
    recipe = read('art-source/editable/ART-10/generation-inputs.json')
    for attempt in recipe['attempts']:
        attempt['sha256'] = sha(attempt['generated_file'])
        with Image.open(attempt['generated_file']) as image:
            attempt['actual_size_px'] = list(image.size)
            attempt['mode'] = image.mode
        attempt['input_hashes'] = [{'path': p, 'sha256': sha(p)} for p in attempt['input_paths']]
    export = read('docs/art/qa/ART-10-export.json')
    assert export['output_sha256'] == sha(final)
    selected = next(a for a in recipe['attempts'] if a.get('selected'))
    record = {
        'id': 'MAP_BASE', 'output_path': manifest['asset_root'] + '/' + asset['path'],
        'origin': 'Ilustração original gerada pela ferramenta integrada ImageGen com referência ao concept aprovado do hub; revisão de vegetação baixa pela mesma ferramenta.',
        'references': [{'path': p, 'sha256': sha(ROOT / p)} for p in asset['references']],
        'requested_size_px': [1920, 1080], 'actual_size_px': [1920, 1080], 'mode': 'RGBA',
        'color_space': 'sRGB', 'sha256': sha(final), 'transparent_background': True,
        'final_prompt': selected['prompt'], 'attempts': recipe['attempts'], 'technical_export': export,
        'tool': recipe['tool'], 'model': None, 'model_note': recipe['model_note'],
        'status': 'gerado', 'coordinate_contract': 'docs/art/layouts/map-layout.json',
        'coordinate_contract_sha256': sha(ROOT / 'docs/art/layouts/map-layout.json'),
        'source_editable': None,
        'source_note': 'Arte raster generativa; sem fonte vetorial fictícia. Prompts e script de export técnico são preservados.',
        'pivot': [0, 1], 'nine_slice_lbrt_px': [0, 0, 0, 0]
    }
    write('docs/art/prompts/ART-10.json', {
        'task_id': TASK, 'date': '2026-10-14', 'status': 'gerado', 'tool': recipe['tool'],
        'mode': 'built-in', 'model': None, 'model_note': recipe['model_note'],
        'scope': 'Somente MAP_BASE e map-layout.json. Cinco IDs de localidades e implantação central; nenhum controle do ART-05 ou construção/realce dos lotes futuros.',
        'assets': [record], 'auxiliary_deliverables': ['docs/art/layouts/map-layout.json'],
        'producer_source': 'art-source/editable/ART-10/record-art10.py',
        'export_source': 'art-source/editable/ART-10/export-map.cjs',
        'preview_source': 'art-source/editable/qa/ART-10-preview.py', 'integration_performed': False
    })
    asset.update({
        'status': 'gerado', 'source': record['origin'], 'sha256': record['sha256'],
        'actual_size_px': record['actual_size_px'], 'generation_prompt': record['final_prompt'],
        'generation_tool': record['tool'], 'generation_model': None, 'generation_model_note': recipe['model_note'],
        'production_method': 'imagegen', 'production_record': 'docs/art/prompts/ART-10.json',
        'display_fit': 'contain: todas as camadas do mapa compartilham a transformação uniforme de map-layout.json',
        'coordinate_contract': 'docs/art/layouts/map-layout.json',
        'alpha_note': 'Alfa zero externo e alfa original quase opaco no miolo (250–254 nos lotes), preservado sem clamping. Transparência real conforme pedido do usuário.'
    })
    next(t for t in manifest['tasks'] if t['id'] == TASK)['status'] = 'gerado'
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id'] == TASK)['status'] = 'gerado'
    write('docs/art/manifest.json', manifest)
    write('docs/art/tasks.json', tasks)
    print(json.dumps({'task_id': TASK, 'status': 'gerado', 'png_count': 1, 'auxiliary_count': 1}))

def finalize():
    manifest = read('docs/art/manifest.json')
    technical, measured = read('docs/art/qa/ART-10-technical.json'), read('docs/art/qa/ART-10-measurements.json')
    assert technical['passed'] and technical['asset_count'] == 1 and not technical['errors']
    assert measured['passed'] and measured['schema_valid'] and measured['nonoverlapping_polygons']
    production = read('docs/art/prompts/ART-10.json')
    asset = next(a for a in manifest['assets'] if a['task_id'] == TASK)
    assert sha(ROOT / manifest['asset_root'] / asset['path']) == asset['sha256'] == measured['sha256']
    qa = {
        'task_id': TASK, 'date': '2026-10-14', 'status': 'validado',
        'reviewer': 'Codex — inspeção visual do agente',
        'technical_validation': {'passed': True, 'asset_count': 1,
            'command': 'python tools/art/validate_assets.py --project D:/Cogna/jogos_empresa --task ART-10',
            'report': 'docs/art/qa/ART-10-technical.json', 'errors': []},
        'visual_validation': {
            'passed': True, 'backgrounds': ['#F5F0E8', '#24113D'],
            'export_scale': 'Export 1920x1080 inspecionado em 1:1, incluindo regiões de borda.',
            'display_method': 'Contain uniforme 2/3, mapa 1280x720 dentro da caixa 1280x848; offset vertical local 64 px.',
            'previews': measured['previews'],
            'checks': [
                'Câmera oblíqua editorial, ruas diagonais, rio em curva e duas pontes coerentes com o hub aprovado.',
                'Cinco zonas e restaurante central identificáveis por geografia; lotes vazios, sem prédios, nomes, pinos, HUD ou realces.',
                'Vegetação baixa fora das implantações; caminhos internos residenciais preservados para futuras casas.',
                'Polígonos anotados conferidos sobre o master; sem sobreposição nem inclusão de vias públicas.',
                'Terreno central e âncora de porta reservados para as três variantes de restaurante, com footprint/escala compartilhados.',
                'PNG RGBA8 com perfil sRGB e dimensões exatas; alfa zero externo e miolo visualmente sólido com alfa original quase opaco, sem checkerboard incorporado.',
                'Bordas e recorte inspecionados sobre marfim/ameixa; sem contorno externo de UI ou sombra de pedestal.',
                'Composição de exibição preserva proporção e todas as implantações; nenhum stretch independente ou crop.',
                'Contrato compatível com o schema, com cinco IDs do catálogo, hit-areas, labels, camadas, pivot, câmera, luz e remapeamento futuro.'
            ]
        },
        'asset': {'id': 'MAP_BASE', 'path': manifest['asset_root'] + '/' + asset['path'],
            'actual_size_px': [1920, 1080], 'sha256': asset['sha256'], 'technical': True, 'visual': True},
        'coordinate_contract': 'docs/art/layouts/map-layout.json',
        'contract_measurements': 'docs/art/qa/ART-10-measurements.json',
        'scene_snapshot': 'docs/art/qa/ART-10-scene-snapshot.json',
        'normalization_note': 'ImageGen devolveu 1672x941; export técnico uniformemente escalado para 1920x1080 com padding transparente de arredondamento, preservando toda a arte e o alfa.',
        'alpha_note': measured['alpha_note'],
        'pending': [], 'integration_performed': False,
        'future_work': 'Construções ART-11, restaurantes/foreground ART-12 e realces/marcadores ART-13 usam o contrato; seguem planejados.'
    }
    write('docs/art/qa/ART-10.json', qa)
    asset['status'] = 'validado'
    asset['alpha_note'] = measured['alpha_note']
    asset['qa'] = {'technical': True, 'visual': True, 'identity': False,
        'identity_note': 'Cenário sem personagens.', 'record': 'docs/art/qa/ART-10.json',
        'preview': 'art-source/previews/ART-10/ART-10-contact-sheet.png',
        'details': 'art-source/previews/ART-10/map-layout-annotated.png',
        'notes': 'Geografia nova e contrato de implantação revisados; transparência externa real e seis espaços vazios.'}
    next(t for t in manifest['tasks'] if t['id'] == TASK).update({'status': 'validado', 'qa_record': 'docs/art/qa/ART-10.json'})
    tasks = read('docs/art/tasks.json')
    next(t for t in tasks if t['id'] == TASK).update({'status': 'validado', 'qa_record': 'docs/art/qa/ART-10.json'})
    production['status'] = 'validado'
    production['qa_record'] = 'docs/art/qa/ART-10.json'
    production['assets'][0]['status'] = 'validado'
    production['assets'][0]['alpha_note'] = measured['alpha_note']
    contract = read('docs/art/layouts/map-layout.json')
    contract['layers'][0]['status'] = 'validado'
    write('docs/art/layouts/map-layout.json', contract)
    production['assets'][0]['coordinate_contract_sha256'] = sha(ROOT / 'docs/art/layouts/map-layout.json')
    manifest['phase'] = 'produção por lotes; ART-01 a ART-10 validados'
    write('docs/art/manifest.json', manifest)
    write('docs/art/tasks.json', tasks)
    write('docs/art/prompts/ART-10.json', production)
    print(json.dumps({'task_id': TASK, 'status': 'validado', 'png_count': 1, 'auxiliary_count': 1, 'pending': []}))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['generated', 'finalize'])
    parser.add_argument('--visual-reviewed', action='store_true')
    args = parser.parse_args()
    if args.mode == 'finalize' and not args.visual_reviewed:
        parser.error('Finalization requires completed visual inspection.')
    generated() if args.mode == 'generated' else finalize()
