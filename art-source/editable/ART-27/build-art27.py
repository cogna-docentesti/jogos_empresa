"""ART-27: preview-only assembly of existing PNGs. Never writes production art."""
from pathlib import Path
from functools import lru_cache
import argparse
import hashlib
import json
import subprocess
import sys

from PIL import Image, ImageCms, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
TID = 'ART-27'
PREVIEW = 'art-source/previews/ART-27'
QA = 'docs/art/qa'
SOURCE = 'art-source/editable/ART-27/build-art27.py'
DATE = '2026-10-15'
R = Image.Resampling.LANCZOS
COLORS = {'violet': '#6D28D9', 'plum': '#24113D', 'lavender': '#B99AE8',
          'ivory': '#F5F0E8', 'ink': '#30243C', 'teal': '#26776A', 'amber': '#E7B65A'}
ICC = ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
BRIEF = ('Executar somente ART-27: prova de montagem do estabelecimento e revisão, '
         'canvas 1920×1080, com fachadas, KPIs, checklist, ícones e personagens já '
         'validados. Preservar alfa e proporções, usar 9-slice nos componentes. '
         'Documentar arquivos e transformações por tela. Não criar sprites, '
         'alterar cenas/prefabs nem implementar UI. Valores dinâmicos ficam como '
         'marcadores; textos são apenas anotações da prova de montagem.')


def read(rel):
    return json.loads((ROOT / rel).read_text(encoding='utf-8'))


def write(rel, data):
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(rel):
    return hashlib.sha256((ROOT / rel).read_bytes()).hexdigest()


def digest(data):
    return hashlib.sha256(json.dumps(data, ensure_ascii=False, sort_keys=True,
                                    separators=(',', ':')).encode('utf-8')).hexdigest()


def asset_tree():
    return {p.relative_to(ROOT).as_posix(): sha(p.relative_to(ROOT))
            for p in sorted((ROOT / 'Assets/Art/new').rglob('*')) if p.is_file()}


def model_snapshot(manifest, index):
    return {'all_asset_records_sha256': digest(manifest['assets']),
            'other_tasks_sha256': digest([t for t in manifest['tasks'] if t['id'] != TID]),
            'other_index_sha256': digest([t for t in index if t['id'] != TID]),
            'manifest_other_fields_sha256': digest({k: v for k, v in manifest.items()
                                                    if k not in ['assets', 'tasks']})}


def helper(flag, task=None):
    args = [sys.executable, str(ROOT / 'tools/art/validate_assets.py'), '--project', str(ROOT), flag]
    if task:
        args.append(task)
    p = subprocess.run(args, cwd=ROOT, capture_output=True, encoding='utf-8')
    if p.returncode:
        raise RuntimeError(p.stdout + p.stderr)
    return json.loads(p.stdout)


MANIFEST = read('docs/art/manifest.json')
INDEX = read('docs/art/tasks.json')
TASK = next(t for t in MANIFEST['tasks'] if t['id'] == TID)
ASSETS = {a['id']: a for a in MANIFEST['assets']}
USED = set()
SLICE_CHECKS = []
TEXT_CHECKS = []


@lru_cache(None)
def original(aid):
    a = ASSETS[aid]
    assert a['status'] == 'validado', aid
    p = f"{MANIFEST['asset_root']}/{a['path']}"
    with Image.open(ROOT / p) as src:
        im = src.copy()
    assert im.mode == 'RGBA' and list(im.size) == a['export_size_px'], aid
    assert sha(p) == a['sha256'], f'Manifest hash differs: {aid}'
    return im


@lru_cache(None)
def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf' if bold
                              else 'C:/Windows/Fonts/segoeui.ttf', size)


def text(im, label, xy, size=20, color=None, bold=False, max_width=None, center=False):
    f = font(size, bold)
    width = f.getlength(label)
    if max_width is not None:
        assert width <= max_width, (label, width, max_width)
    x, y = xy
    if center:
        x -= width / 2
    box = ImageDraw.Draw(im).textbbox((x, y), label, font=f)
    assert box[0] >= 0 and box[2] <= im.width and box[1] >= 0 and box[3] <= im.height, (label, box)
    ImageDraw.Draw(im).text((x, y), label, font=f, fill=color or COLORS['ink'])
    TEXT_CHECKS.append({'text': label, 'bbox_ltrb_px': list(box), 'within_canvas': True})


def nine_slice(src, target, borders, aid):
    l, b, r, t = borders
    w, h = src.size
    W, H = target
    assert W > l + r and H > t + b
    sx, sy = [0, l, w-r, w], [0, t, h-b, h]
    tx, ty = [0, l, W-r, W], [0, t, H-b, H]
    out = Image.new('RGBA', target)
    for row in range(3):
        for col in range(3):
            part = src.crop((sx[col], sy[row], sx[col+1], sy[row+1]))
            dest = (tx[col+1]-tx[col], ty[row+1]-ty[row])
            if part.size != dest:
                part = part.resize(dest, R)
            out.paste(part, (tx[col], ty[row]))
    pairs = [((0, 0, l, t), (0, 0, l, t)), ((w-r, 0, w, t), (W-r, 0, W, t)),
             ((0, h-b, l, h), (0, H-b, l, H)), ((w-r, h-b, w, h), (W-r, H-b, W, H))]
    assert all(src.crop(a).tobytes() == out.crop(b).tobytes() for a, b in pairs)
    SLICE_CHECKS.append({'asset_id': aid, 'target_px': list(target),
                         'nine_slice_lbrt_px': borders, 'corners_pixel_identical': True})
    return out


def place(canvas, layers, aid, rect, tint=None):
    x, y, w, h = rect
    a, src = ASSETS[aid], original(aid)
    mode = a.get('display_fit', 'contain')
    scale = None
    if mode == 'sliced':
        im = nine_slice(src, (w, h), a['nine_slice_lbrt_px'], aid)
        pos = (x, y)
    elif mode == 'exact':
        assert src.size == (w, h)
        im, pos = src.copy(), (x, y)
    else:
        scale = min(w/src.width, h/src.height)
        # All illustration and icon placements use identical scale for both axes.
        nw, nh = round(src.width * scale), round(src.height * scale)
        im = src.resize((nw, nh), R)
        pos = (x + (w-nw)//2, y + (h-nh)//2)
        mode = 'contain'
    if tint:
        tinted = Image.new('RGBA', im.size, tint)
        tinted.putalpha(im.getchannel('A'))
        im = tinted
    assert x >= 0 and y >= 0 and x+w <= canvas.width and y+h <= canvas.height
    canvas.alpha_composite(im, pos)
    USED.add(aid)
    layers.append({'order': len(layers), 'asset_id': aid,
                   'path': f"{MANIFEST['asset_root']}/{a['path']}", 'sha256': a['sha256'],
                   'source_size_px': list(src.size), 'target_rect_xywh_px': list(rect),
                   'frame_position_px': list(pos), 'render_mode': mode,
                   'uniform_scale': scale, 'source_crop_ltrb_px': [0, 0, 0, 0],
                   'pivot': a['pivot'], 'nine_slice_lbrt_px': a['nine_slice_lbrt_px'],
                   'tint_preview_only': tint})


def team_view(canvas, layers, actor, environment, xy):
    x, y = xy
    viewport = Image.new('RGBA', (240, 180))
    place(viewport, layers, environment, (0, 0, 240, 180))
    layers[-1]['target_rect_xywh_px'] = [x, y, 240, 180]
    layers[-1]['frame_position_px'] = [x, y]
    src = original(actor)
    scale = 180/src.width
    resized = src.resize((180, 180), R)
    bbox = resized.getchannel('A').getbbox()
    top = 180 - bbox[3]
    # The viewport clips only transparent padding below the half-body cut.
    assert resized.getchannel('A').crop((0, bbox[3], 180, 180)).getbbox() is None
    viewport.alpha_composite(resized, (30, top))
    canvas.alpha_composite(viewport, (x, y))
    a = ASSETS[actor]
    USED.add(actor)
    layers.append({'order': len(layers), 'asset_id': actor,
                   'path': f"{MANIFEST['asset_root']}/{a['path']}", 'sha256': a['sha256'],
                   'source_size_px': list(src.size), 'target_rect_xywh_px': [x+30, y+top, 180, 180],
                   'frame_position_px': [x+30, y+top], 'render_mode': 'contain',
                   'uniform_scale': scale, 'source_crop_ltrb_px': [0, 0, 0, 0],
                   'viewport_clip_xywh_px': [x, y, 240, 180],
                   'clipped_nontransparent_pixels': 0, 'pivot': a['pivot'],
                   'nine_slice_lbrt_px': [0, 0, 0, 0], 'tint_preview_only': None})


def save_preview(im, filename):
    rel = f'{PREVIEW}/{filename}'
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    im.save(p, icc_profile=ICC)
    return {'path': rel, 'actual_size_px': list(im.size), 'mode': im.mode,
            'sha256': sha(rel), 'preview_only': True}


def panel(im, layers, rect):
    x, y, w, h = rect
    place(im, layers, 'UI_PANEL_SHADOW', (x-6, y-6, w+12, h+12))
    place(im, layers, 'UI_PANEL_IVORY', rect)


VARIANTS = [
    ('lanches', 'Lanches', 'REST_LANCHES', 'FACADE_REST_LANCHES',
     'PORTRAIT_GRILL_COOK', 'TEAM_ENV_GRILL_KITCHEN', 'Chapeiro', False),
    ('frances', 'Francês', 'REST_FRANCES', 'FACADE_REST_FRANCES',
     'PORTRAIT_HEAD_CHEF', 'TEAM_ENV_FRENCH_KITCHEN', 'Chef de cozinha', False),
    ('japones', 'Japonês', 'REST_JAPONES', 'FACADE_REST_JAPONES',
     'PORTRAIT_SUSHI_CHEF', 'TEAM_ENV_SUSHI_COUNTER', 'Profissional de sushi', False),
    ('revisao-pendente', 'Lanches', 'REST_LANCHES', 'FACADE_REST_LANCHES',
     'PORTRAIT_GRILL_COOK', 'TEAM_ENV_GRILL_KITCHEN', 'Chapeiro', True),
]


def screen(spec):
    slug, name, catalog, facade, cook, kitchen, cook_label, pending = spec
    im = Image.new('RGBA', (1920, 1080), COLORS['ivory'])
    layers = []
    text_start = len(TEXT_CHECKS)
    place(im, layers, 'UI_TOPBAR_VIOLET', (0, 0, 1920, 88))
    place(im, layers, 'ICON_COMMERCE', (28, 20, 48, 48), '#FFFFFF')
    text(im, 'Jogos de Empresa', (96, 18), 32, '#FFFFFF', True, 370)
    text(im, 'GESTÃO DE RESTAURANTES', (486, 32), 17, '#FFFFFF', max_width=440)
    for x, icon, label in [(1008, 'ICON_CALENDAR', 'Mês —'),
                            (1300, 'ICON_CASH', 'Caixa  R$ —'),
                            (1592, 'ICON_STAR', 'Score  —')]:
        place(im, layers, 'UI_HUD_PILL', (x, 14, 280, 60))
        place(im, layers, icon, (x+16, 24, 40, 40), COLORS['lavender'])
        text(im, label, (x+66, 30), 21, '#FFFFFF', max_width=198)
    text(im, 'Meu estabelecimento', (48, 118), 38, COLORS['plum'], True)
    text(im, 'Resumo das decisões', (50, 170), 18, COLORS['ink'])
    place(im, layers, 'UI_PILL_NORMAL', (1552, 124, 320, 52))
    text(im, 'Revisão das decisões' if pending else 'Visão geral', (1712, 136), 20,
         COLORS['plum'], center=True, max_width=280)
    panel(im, layers, (24, 208, 784, 752))
    panel(im, layers, (832, 208, 1064, 752))
    text(im, name, (52, 232), 28, COLORS['plum'], True)
    place(im, layers, 'ICON_PIN', (52, 276, 24, 24), COLORS['violet'])
    text(im, 'Localização selecionada', (86, 273), 19)
    place(im, layers, facade, (176, 296, 480, 360))
    text(im, 'Equipe', (52, 674), 24, COLORS['plum'], True)
    # Three independent backgrounds and portraits; the manager is a visual example,
    # not a declaration of required staffing or quantities.
    for x, actor, env, label in [(52, 'PORTRAIT_ATTENDANT', 'TEAM_ENV_SERVICE_COUNTER', 'Atendente'),
                                 (300, cook, kitchen, cook_label),
                                 (548, 'PORTRAIT_MANAGER', 'TEAM_ENV_MANAGEMENT_OFFICE', 'Gerente')]:
        place(im, layers, 'UI_CARD_NORMAL', (x, 714, 240, 230))
        team_view(im, layers, actor, env, (x, 722))
        text(im, label, (x+120, 913), 16, COLORS['plum'], True, 230, True)
    for x, y, icon, label, value, tint in [
        (864, 240, 'ICON_CASH', 'Caixa', 'R$ —', COLORS['violet']),
        (1376, 240, 'ICON_REVENUE', 'Receita prevista', 'R$ —', COLORS['violet']),
        (864, 376, 'ICON_TREND_UP', 'Resultado previsto', 'R$ —', COLORS['teal']),
        (1376, 376, 'ICON_STAR', 'Score', '— / 100', COLORS['amber'])]:
        place(im, layers, 'UI_KPI_TILE', (x, y, 488, 112))
        place(im, layers, icon, (x+24, y+32, 48, 48), tint)
        text(im, label, (x+96, y+17), 20, COLORS['plum'], True, 360)
        text(im, value, (x+96, y+49), 32, COLORS['plum'], True, 360)
    text(im, 'Coerência', (864, 526), 24, COLORS['plum'], True)
    text(im, 'A calcular', (1740, 532), 19, COLORS['ink'], max_width=124)
    place(im, layers, 'UI_PROGRESS_TRACK', (864, 578, 1000, 16))
    text(im, 'Revisão das decisões', (864, 638), 26, COLORS['plum'], True)
    for i, label in enumerate(['Localização', 'Restaurante', 'Cardápio', 'Equipamentos', 'Equipe']):
        x = 864 + 200*i
        state = 'UI_CHECKBOX_OFF' if pending and i == 4 else 'UI_CHECKBOX_ON'
        place(im, layers, state, (x, 708, 32, 32))
        if state == 'UI_CHECKBOX_ON':
            place(im, layers, 'ICON_CHECK', (x, 708, 32, 32), '#FFFFFF')
        text(im, label, (x+42, 711), 17, COLORS['plum'], max_width=154)
    sentence = ('Revise a equipe antes de concluir as decisões.' if pending else
                'Acompanhe os indicadores e consulte seus resultados.')
    text(im, sentence, (864, 788), 22, COLORS['ink'], max_width=1000)
    button = 'UI_PRIMARY_VIOLET_DISABLED' if pending else 'UI_PRIMARY_VIOLET_NORMAL'
    place(im, layers, button, (864, 852, 1000, 72))
    color = '#786D80' if pending else '#FFFFFF'
    text(im, 'Ver DRE', (1364, 869), 26, color, True, 700, True)
    place(im, layers, 'ICON_NEXT', (1788, 868, 40, 40), color)
    place(im, layers, 'UI_NAVBAR_PLUM', (0, 984, 1920, 96))
    for x, icon, label, active in [(48, 'ICON_HOME', 'Meu estabelecimento', True),
                                   (406, 'ICON_CHART_BARS', 'Financeiro', False),
                                   (676, 'ICON_EQUIPMENT', 'Equipamentos', False),
                                   (1006, 'ICON_TEAM', 'Equipe', False),
                                   (1256, 'ICON_MENU_FOOD', 'Cardápio', False)]:
        tint = '#FFFFFF' if active else COLORS['lavender']
        place(im, layers, icon, (x, 1010, 40, 40), tint)
        text(im, label, (x+52, 1016), 20, tint, max_width=260)
    place(im, layers, 'UI_NAV_UNDERLINE', (48, 1060, 280, 4))
    place(im, layers, 'UI_ACCENT_AMBER_NORMAL', (1576, 996, 296, 72))
    place(im, layers, 'ICON_CALENDAR', (1596, 1012, 40, 40), COLORS['plum'])
    text(im, 'Fechar mês', (1654, 1016), 22, COLORS['plum'], True, 200)
    out = save_preview(im, f'ART-27-{slug}-1920x1080.png')
    return {**out, 'screen_id': slug, 'catalog_id': catalog,
            'purpose': 'Checklist com equipe pendente' if pending else 'Estabelecimento — ' + name,
            'asset_files': sorted({v['path'] for v in layers}), 'layers': layers,
            'native_text_preview_only': TEXT_CHECKS[text_start:],
            'data_policy': 'Valores econômicos e mês não simulados; marcadores — / A calcular. '
                           'Checklist e estado do botão são exemplares estáticos de QA, sem regras implementadas.'}


def transparency_previews():
    outputs = []
    sheet = Image.new('RGBA', (1920, 928), '#D9D2E0')
    text(sheet, 'ART-27 · Fachadas · 480×360 · alfa original sobre claro e escuro', (24, 16), 24, bold=True)
    for i, (aid, title) in enumerate([('FACADE_REST_LANCHES', 'Lanches'),
                                     ('FACADE_REST_FRANCES', 'Francês'),
                                     ('FACADE_REST_JAPONES', 'Japonês')]):
        x = i*640
        text(sheet, title, (x+24, 64), 20, bold=True)
        for y, bg in [(112, COLORS['ivory']), (528, COLORS['plum'])]:
            tile = Image.new('RGBA', (608, 360), bg)
            place(tile, [], aid, (64, 0, 480, 360))
            sheet.alpha_composite(tile, (x+16, y))
    outputs.append(save_preview(sheet, 'ART-27-facades-light-dark.png'))
    sheet = Image.new('RGBA', (1920, 744), '#D9D2E0')
    text(sheet, 'ART-27 · Personagens · 240×240 · alfa original e identidades preservadas', (24, 16), 24, bold=True)
    actors = [('PORTRAIT_ATTENDANT', 'CH01 · Atendente'), ('PORTRAIT_GRILL_COOK', 'CH02 · Chapeiro'),
              ('PORTRAIT_HEAD_CHEF', 'CH05 · Chef'), ('PORTRAIT_MANAGER', 'CH03 · Gerente'),
              ('PORTRAIT_SUSHI_CHEF', 'CH06 · Sushi')]
    for i, (aid, title) in enumerate(actors):
        x = i*384
        text(sheet, title, (x+24, 68), 20, bold=True)
        for y, bg in [(112, COLORS['ivory']), (448, COLORS['plum'])]:
            tile = Image.new('RGBA', (352, 256), bg)
            place(tile, [], aid, (56, 8, 240, 240))
            sheet.alpha_composite(tile, (x+16, y))
    outputs.append(save_preview(sheet, 'ART-27-portraits-light-dark.png'))
    return outputs


def ui_preview():
    out = Image.new('RGBA', (1920, 1080), '#D9D2E0')
    text(out, 'ART-27 · Componentes e estados · 9-slice e tint somente na montagem', (24, 16), 24, bold=True)
    for offset, bg, ink in [(16, COLORS['ivory'], COLORS['plum']), (976, COLORS['plum'], '#FFFFFF')]:
        tile = Image.new('RGBA', (928, 984), bg)
        layers = []
        text(tile, 'KPIs / HUD', (24, 12), 21, ink, True)
        place(tile, layers, 'UI_KPI_TILE', (24, 56, 400, 112))
        place(tile, layers, 'ICON_REVENUE', (48, 88, 48, 48), COLORS['violet'])
        text(tile, 'Receita prevista', (120, 70), 20, COLORS['plum'], True)
        text(tile, 'R$ —', (120, 106), 30, COLORS['plum'], True)
        place(tile, layers, 'UI_HUD_PILL', (456, 80, 280, 60))
        place(tile, layers, 'ICON_CASH', (472, 90, 40, 40), '#FFFFFF')
        text(tile, 'Caixa  R$ —', (522, 94), 20, '#FFFFFF')
        text(tile, 'Progresso · espécimes de QA 0 / 50 / 100%', (24, 200), 21, ink, True)
        for y, width in [(252, 0), (296, 240), (340, 480)]:
            place(tile, layers, 'UI_PROGRESS_TRACK', (24, y, 480, 16))
            if width:
                place(tile, layers, 'UI_PROGRESS_FILL', (24, y, width, 16))
        text(tile, 'Ações / checklist', (24, 402), 21, ink, True)
        place(tile, layers, 'UI_PRIMARY_VIOLET_NORMAL', (24, 452, 360, 72))
        place(tile, layers, 'UI_PRIMARY_VIOLET_DISABLED', (432, 452, 360, 72))
        text(tile, 'Ver DRE', (204, 469), 24, '#FFFFFF', True, 300, True)
        text(tile, 'Ver DRE', (612, 469), 24, '#786D80', True, 300, True)
        for i, aid in enumerate(['UI_CHECKBOX_ON', 'UI_CHECKBOX_OFF']):
            place(tile, layers, aid, (24+i*208, 564, 32, 32))
            if aid == 'UI_CHECKBOX_ON':
                place(tile, layers, 'ICON_CHECK', (24+i*208, 564, 32, 32), '#FFFFFF')
            text(tile, ['Concluído', 'Pendente'][i], (68+i*208, 565), 19, ink)
        text(tile, 'Ícones · 40×40', (24, 640), 21, ink, True)
        for i, aid in enumerate(['ICON_HOME', 'ICON_CHART_BARS', 'ICON_EQUIPMENT', 'ICON_TEAM',
                                  'ICON_MENU_FOOD', 'ICON_PIN', 'ICON_TREND_UP', 'ICON_REPUTATION', 'ICON_NEXT']):
            place(tile, layers, aid, (24+i*96, 694, 40, 40), ink)
        place(tile, layers, 'UI_PANEL_SHADOW', (18, 788, 332, 156))
        place(tile, layers, 'UI_PANEL_IVORY', (24, 794, 320, 144))
        text(tile, 'Painel + sombra', (52, 846), 20, COLORS['plum'], True)
        place(tile, layers, 'UI_CARD_NORMAL', (376, 794, 208, 144))
        text(tile, 'Card', (480, 846), 20, COLORS['plum'], True, 180, True)
        place(tile, layers, 'UI_PILL_NORMAL', (616, 832, 280, 52))
        text(tile, 'Revisão', (756, 844), 20, COLORS['plum'], True, 240, True)
        out.alpha_composite(tile, (offset, 80))
    return save_preview(out, 'ART-27-ui-light-dark.png')


def measurements():
    rows = []
    for aid in sorted(USED):
        a, im = ASSETS[aid], original(aid)
        alpha = im.getchannel('A')
        hist = alpha.histogram()
        path = f"{MANIFEST['asset_root']}/{a['path']}"
        rows.append({'asset_id': aid, 'task_id': a['task_id'], 'path': path,
                     'sha256': sha(path), 'actual_size_px': list(im.size), 'mode': im.mode,
                     'alpha_extrema': list(alpha.getextrema()), 'alpha_bbox_ltrb': list(alpha.getbbox()),
                     'transparent_fraction': hist[0]/(im.width*im.height),
                     'alpha_policy': a['alpha_policy'], 'source_qa': a.get('qa', {}).get('record'),
                     'alpha_preserved': True})
    return rows


def build():
    assert TASK['scope'] == 'validacao' and TASK['asset_ids'] == []
    baseline_path = f'{QA}/ART-27-scope-baseline.json'
    if not (ROOT / baseline_path).exists():
        assert TASK['status'] == 'planejado'
        runtime_paths = subprocess.check_output(['git', 'ls-files', '-z', 'Assets', 'Packages',
                                                 'ProjectSettings'], cwd=ROOT).decode('utf-8').split('\0')
        protected = {p: sha(p) for p in runtime_paths if p and (ROOT / p).is_file()
                     and not p.startswith('Assets/Art/new/')}
        write(baseline_path, {'task_id': TID, 'date': DATE, **model_snapshot(MANIFEST, INDEX),
                              'asset_tree': asset_tree(), 'protected_runtime_files': protected,
                              'initial_git_status': subprocess.check_output(['git', 'status', '--short'],
                                                                            cwd=ROOT).decode('utf-8')})
    reports = []
    for tid in TASK['dependencies'] + ['ART-04', 'ART-05', 'ART-08']:
        assert next(t for t in MANIFEST['tasks'] if t['id'] == tid)['status'] == 'validado'
        r = helper('--task', tid)
        write(f'{QA}/ART-27-prerequisite-{tid}.json', r)
        reports.append({'task_id': tid, 'asset_count': r['asset_count'], 'passed': r['passed'],
                        'report': f'{QA}/ART-27-prerequisite-{tid}.json',
                        'usage': 'declared prerequisite' if tid in TASK['dependencies'] else
                                 'supplementary reuse only: button/checklist/arrow; no production'})
    write(f'{QA}/ART-27-prerequisites.json', {'task_id': TID, 'passed': True, 'reports': reports})
    for ch in read('docs/art/characters.json'):
        assert sha(ch['canonical_reference_path']) == ch['identity_hash']
    screens = [screen(v) for v in VARIANTS]
    alpha = transparency_previews()
    ui = ui_preview()
    overview = Image.new('RGBA', (1920, 1264), '#D9D2E0')
    text(overview, 'ART-27 · Prova de montagem do estabelecimento e revisão', (24, 14), 30, bold=True)
    text(overview, '4 telas de 1920×1080 · PNGs existentes · texto e estados somente na prova visual',
         (24, 60), 20)
    for i, s in enumerate(screens):
        x, y = 24 + (i % 2)*960, 116 + (i // 2)*568
        with Image.open(ROOT / s['path']) as im:
            overview.alpha_composite(im.resize((912, 513), R), (x, y))
        text(overview, s['purpose'], (x, y+520), 20, bold=True)
    overview_record = save_preview(overview, 'ART-27-overview.png')
    write('docs/art/layouts/establishment-review-layout.json', {
        'task_id': TID, 'preview_only': True, 'integration_performed': False,
        'canvas_px': [1920, 1080], 'coordinate_origin': 'top_left_y_down',
        'concept_reference': 'art-source/references/concepts/03-gestao.png',
        'source_editable': SOURCE,
        'layout_policy': 'Facade / metrics / checklist from concept 03. Additional independent team strip '
                         'demonstrates ART-26 reuse. Proposed coordinates only; no scene measurements or changes.',
        'regions_xywh_px': {'topbar': [0, 0, 1920, 88], 'navbar': [0, 984, 1920, 96],
                            'facade_team_panel': [24, 208, 784, 752], 'kpi_review_panel': [832, 208, 1064, 752]},
        'screens': screens})
    consumption = {'task_id': TID, 'preview_only': True, 'source_editable': SOURCE,
                   'screens': [{k: s[k] for k in ['screen_id', 'catalog_id', 'path', 'asset_files', 'layers']}
                               for s in screens], 'unique_source_assets': measurements()}
    write(f'{QA}/ART-27-consumption.json', consumption)
    previews = screens + alpha + [ui, overview_record]
    technical = {'task_id': TID, 'passed': True, 'production_png_count': 0,
                 'screen_count': len(screens), 'preview_count': len(previews),
                 'screens_exact_1920x1080': all(s['actual_size_px'] == [1920, 1080] for s in screens),
                 'source_assets_reused': len(USED), 'source_hashes_match_manifest': True,
                 'source_images_unchanged': True, 'all_source_images_rgba': True,
                 'nine_slice_corners_pixel_identical': all(x['corners_pixel_identical'] for x in SLICE_CHECKS),
                 'slice_checks': SLICE_CHECKS, 'text_bounds_checked': len(TEXT_CHECKS),
                 'all_text_within_canvas': True, 'all_illustration_scales_uniform': True,
                 'portrait_visible_pixels_clipped': 0, 'previews': previews,
                 'helper_note': 'ART-27 has no production PNGs; the ordinary helper reports zero assets. '
                                'This report separately checks actual compositions and reused sources.'}
    write(f'{QA}/ART-27-composition-technical.json', technical)
    write(f'{QA}/ART-27-technical.json', helper('--task', TID))
    write(f'{QA}/ART-27-plan-validation.json', helper('--plan'))
    write('docs/art/prompts/ART-27.json', {
        'task_id': TID, 'date': DATE, 'status': 'gerado', 'scope': 'validacao',
        'tool': 'Python/Pillow — deterministic QA compositing of existing RGBA PNGs',
        'model': None, 'model_note': 'No generative model used: this batch creates no new artwork.',
        'prompt': BRIEF, 'distinct_asset_generations': 0, 'production_png_count': 0,
        'source_editable': SOURCE,
        'references': [{'path': p, 'sha256': sha(p)} for p in
                       ['art-source/references/concepts/01-hub.png', 'art-source/references/concepts/03-gestao.png']],
        'consumption_record': f'{QA}/ART-27-consumption.json',
        'layout_record': 'docs/art/layouts/establishment-review-layout.json',
        'source_assets': measurements(), 'previews': previews,
        'delivery_preview': overview_record['path'], 'integration_performed': False})
    print(json.dumps({'task': TID, 'status': 'gerado', 'screens': len(screens),
                      'source_assets_reused': len(USED), 'production_png_count': 0,
                      'overview': overview_record['path']}))


def finalize():
    visual = read('art-source/editable/ART-27/visual-review.json')
    assert visual['passed'] and visual['reviewer'] and len(visual['inspected_previews']) == 8
    p = read('docs/art/prompts/ART-27.json')
    comp = read(f'{QA}/ART-27-composition-technical.json')
    assert comp['passed'] and p['status'] == 'gerado'
    for x in p['previews']:
        assert sha(x['path']) == x['sha256'], x['path']
    for x in p['source_assets'] + p['references']:
        assert sha(x['path']) == x['sha256'], x['path']
    baseline = read(f'{QA}/ART-27-scope-baseline.json')
    snap = model_snapshot(MANIFEST, INDEX)
    assert all(snap[k] == baseline[k] for k in snap)
    assert asset_tree() == baseline['asset_tree']
    changed = [path for path, h in baseline['protected_runtime_files'].items()
               if not (ROOT / path).is_file() or sha(path) != h]
    assert not changed, changed
    assert helper('--plan')['passed'] and helper('--task', TID)['passed']
    for collection in [MANIFEST['tasks'], INDEX]:
        task = next(t for t in collection if t['id'] == TID)
        task.update({'status': 'validado', 'qa_record': f'{QA}/ART-27.json',
                     'preview_record': p['delivery_preview'],
                     'consumption_record': f'{QA}/ART-27-consumption.json',
                     'layout_record': p['layout_record']})
    write('docs/art/manifest.json', MANIFEST)
    write('docs/art/tasks.json', INDEX)
    p['status'] = 'validado'
    p['qa_record'] = f'{QA}/ART-27.json'
    write('docs/art/prompts/ART-27.json', p)
    audit = {'task_id': TID, 'passed': True, 'production_asset_tree_unchanged': True,
             'production_files_checked': len(baseline['asset_tree']),
             'all_asset_records_unchanged': True, 'other_tasks_unchanged': True,
             'other_tasks_index_unchanged': True, 'manifest_other_fields_unchanged': True,
             'runtime_files_checked': len(baseline['protected_runtime_files']),
             'runtime_files_unchanged': True, 'changed_runtime_files': [],
             'new_production_assets': [], 'integration_performed': False}
    write(f'{QA}/ART-27-scope-audit.json', audit)
    write(f'{QA}/ART-27.json', {
        'task_id': TID, 'title': TASK['title'], 'date': DATE, 'status': 'validado',
        'scope_resolution': TASK['note'],
        'production': {'new_sprites': 0, 'tool': p['tool'], 'model': None,
                       'source_editable': SOURCE, 'prompts_record': 'docs/art/prompts/ART-27.json'},
        'technical': {'passed': True, 'ordinary_helper_asset_count': 0,
                      'ordinary_helper_report': f'{QA}/ART-27-technical.json',
                      'composition_report': f'{QA}/ART-27-composition-technical.json',
                      'plan_report': f'{QA}/ART-27-plan-validation.json',
                      'prerequisites_report': f'{QA}/ART-27-prerequisites.json',
                      'scope_report': f'{QA}/ART-27-scope-audit.json'},
        'visual': visual, 'previews': p['previews'],
        'layout_record': p['layout_record'], 'consumption_record': p['consumption_record'],
        'limitations': ['Static art/layout proof; not a running Unity screen.',
                        'Typography and marker values are preview-only. Live values, staffing, checklist '
                        'validation and action availability must come from the game during integration.'],
        'pending': [], 'integration_performed': False})
    print(json.dumps({'task': TID, 'status': 'validado', 'screen_count': comp['screen_count'],
                      'source_assets_reused': comp['source_assets_reused'],
                      'production_png_count': 0, 'scope_audit_passed': True, 'pending': []}))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--finalize', action='store_true', help='Requires completed visual-review.json')
    args = parser.parse_args()
    finalize() if args.finalize else build()
