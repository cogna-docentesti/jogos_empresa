"""Register the six approved systemic-event illustrations without modifying their pixels."""
from pathlib import Path
import copy
import hashlib
import json
import re
import shutil
import uuid
from PIL import Image

PROJECT = Path(__file__).resolve().parents[2]
TASK = "ART-41"


def main():
    prompt_path = PROJECT / "docs/art/prompts/ART-41.json"
    record = json.loads(prompt_path.read_text(encoding="utf-8"))
    manifest_path = PROJECT / "docs/art/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    catalog_path = PROJECT / "docs/art/catalogos.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
    names = {
        "staff_absent": "Funcionário ausente", "staff_resigned": "Funcionário pediu demissão",
        "kitchen_equipment_failure": "Equipamento de cozinha quebrou",
        "equipment_still_down": "Equipamento continua parado",
        "cash_shortfall": "Caixa insuficiente para a parcela",
        "quality_complaint": "Reclamação sobre a qualidade",
    }
    template = next(a for a in manifest["assets"] if a["catalog_id"] == "freezer_breakdown")
    meta_template = (PROJECT / "Assets/Art/new/events/illustrations/freezer_breakdown.png.meta").read_text()
    qa = []
    for item in record["assets"]:
        event_id = item["id"]
        art_id = "EVENT_" + event_id.upper()
        relative = "events/illustrations/" + event_id + ".png"
        output = PROJECT / "Assets/Art/new" / relative
        item["source"] = item["source"].split(" as ")[-1]
        original = Path(item["source"])
        if output.exists() and output.read_bytes() != original.read_bytes():
            raise RuntimeError("Refusing to replace existing artwork: " + str(output))
        shutil.copy2(original, output)
        source = PROJECT / "art-source/editable/ART-41/generations" / output.name
        source.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, source)
        with Image.open(output) as image:
            assert image.mode == "RGBA"
            alpha = image.getchannel("A")
            histogram = alpha.histogram()
            assert histogram[0] > 0 and alpha.getextrema()[1] > 240
            size = list(image.size)
            stats = {"mode": image.mode, "size": size, "alpha_extrema": list(alpha.getextrema()),
                     "transparent_fraction": histogram[0] / (image.width * image.height),
                     "alpha_bbox": list(alpha.getbbox())}
        digest = hashlib.sha256(output.read_bytes()).hexdigest()
        meta = Path(str(output) + ".meta")
        if not meta.exists():
            content = re.sub(r"(?m)^guid: .*", "guid: " + uuid.uuid4().hex, meta_template)
            content = content.replace("maxTextureSize: 1024", "maxTextureSize: 2048")
            meta.write_text(content, encoding="utf-8")
        if event_id in ["equipment_still_down", "kitchen_equipment_failure"]:
            item["references"] = ["Assets/Art/new/events/illustrations/freezer_breakdown.png"]
        entry = copy.deepcopy(template)
        entry.update({
            "id": art_id, "task_id": TASK, "path": relative, "name": names[event_id], "status": "gerado",
            "export_size_px": size, "actual_size_px": size, "catalog_id": event_id,
            "source": "Assets/Resources/Simulation/SimulationConfig.asset#events/" + event_id,
            "references": item["references"], "brief": item["prompt"].split("Primary request: ")[-1],
            "sha256": digest, "generation_prompt": item["prompt"],
            "source_image": source.relative_to(PROJECT).as_posix(), "source_image_sha256": digest,
            "production_record": "docs/art/prompts/ART-41.json", "qa_record": "docs/art/qa/ART-41.json",
            "qa": {"technical": True, "visual": False, "identity": None},
        })
        old = next((i for i, a in enumerate(manifest["assets"]) if a["id"] == art_id), None)
        if old is None: manifest["assets"].append(entry)
        else: manifest["assets"][old] = entry
        if event_id not in [e["id"] for e in catalog["Events"]]:
            catalog["Events"].append({"source": entry["source"], "id": event_id, "name": names[event_id], "sprite": relative,
                "data": {"id": event_id, "title": names[event_id], "source_kind": "systemic_config"}})
        item.update({"asset_id": art_id, "path": output.relative_to(PROJECT).as_posix(), "actual_size_px": size, "sha256": digest})
        qa.append({"id": art_id, "path": item["path"], "sha256": digest, **stats})
    task = {"id": TASK, "title": record["title"], "dependencies": ["ART-32"], "scope": "nucleo", "status": "gerado",
        "note": "Somente os seis eventos do motor novo sem arte; nenhum asset existente foi substituído.",
        "asset_ids": [a["asset_id"] for a in record["assets"]], "auxiliary_deliverables": [],
        "request_prompt": "Revisar todos os eventos e gerar as imagens ausentes seguindo o modelo existente.",
        "production_record": "docs/art/prompts/ART-41.json", "qa_record": "docs/art/qa/ART-41.json",
        "delivery_preview": "art-source/previews/ART-41/index.html"}
    manifest["tasks"] = [t for t in manifest["tasks"] if t["id"] != TASK] + [task]
    manifest["counts"]["assets_total"] = len(manifest["assets"])
    manifest["counts"]["assets_nucleo"] = sum(a.get("scope") == "nucleo" for a in manifest["assets"])
    manifest["counts"]["tasks_total"] = len(manifest["tasks"])
    manifest["catalog_counts"]["Events"] = len(catalog["Events"])
    for path, data in [(manifest_path, manifest), (catalog_path, catalog), (prompt_path, record)]:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    qa_path = PROJECT / "docs/art/qa/ART-41.json"
    qa_path.write_text(json.dumps({"task_id": TASK, "date": "2026-11-25", "status": "gerado", "assets": qa,
        "dimensions_note": "A ferramenta devolveu 1536x1024; os pixels e o alfa originais foram preservados, sem redimensionamento. Display usa contain em 640x426."}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    preview = PROJECT / "art-source/previews/ART-41/index.html"
    preview.parent.mkdir(parents=True, exist_ok=True)
    cards = []
    for item in record["assets"]:
        url = "../../../" + item["path"]
        cards.append(f'<article><h2>{names[item["id"]]}</h2><div class="pair"><div class="light"><img src="{url}"></div><div class="dark"><img src="{url}"></div></div></article>')
    preview.write_text('<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>ART-41 · Eventos do motor</title><style>body{margin:24px;background:#ddd;font:18px Arial;color:#24113d}h1{font-size:26px}h2{font-size:20px;margin:8px}.pair{display:flex;gap:8px}.pair div{width:640px;height:426px}.light{background:#f5f0e8}.dark{background:#24113d}img{width:100%;height:100%;object-fit:contain}article{break-inside:avoid;margin-bottom:20px}</style><h1>ART-41 · Seis eventos, alfa original em fundo claro e escuro</h1>' + ''.join(cards) + '</html>', encoding="utf-8")
    print(json.dumps({"assets": len(qa), "total_events": len(catalog["Events"]), "preview": str(preview)}, ensure_ascii=False))


if __name__ == "__main__": main()
