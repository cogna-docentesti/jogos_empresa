"""Audit every configured/legacy event against its PNG, Sprite importer and runtime registry."""
from pathlib import Path
import argparse
import json
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def audit(sync=False):
    manifest = json.loads((ROOT / "docs/art/manifest.json").read_text(encoding="utf-8"))
    config = (ROOT / "Assets/Resources/Simulation/SimulationConfig.asset").read_text(encoding="utf-8-sig")
    section = config.split("  events:\n", 1)[1]
    configured = set(re.findall(r"^  - id: (\S+)", section, re.M))
    legacy = set()
    for source in (ROOT / "Assets/Resources/Events").glob("*.asset"):
        match = re.search(r"^  id: (\S+)", source.read_text(encoding="utf-8-sig"), re.M)
        if match: legacy.add(match[1])
    assets = {a["catalog_id"]: a for a in manifest["assets"] if a["kind"] == "evento"}
    registry = ROOT / "Assets/Resources/SharedUiSkin.asset"
    serialized = registry.read_text(encoding="utf-8-sig")
    existing = dict(re.findall(r"  - id: (\S+)\n    sprite: \{fileID: 21300000, guid: ([a-f0-9]+), type: 3\}", serialized))
    errors, rows = [], []
    for event_id in sorted(configured | legacy):
        asset = assets.get(event_id)
        if not asset:
            errors.append(event_id + ": missing manifest entry")
            continue
        image = ROOT / "Assets/Art/new" / asset["path"]
        meta = Path(str(image) + ".meta")
        if not image.is_file() or not meta.is_file():
            errors.append(event_id + ": missing PNG/importer")
            continue
        settings = meta.read_text(encoding="utf-8-sig")
        try:
            with Image.open(image) as probe:
                probe.verify()
            with Image.open(image) as probe:
                size = list(probe.size)
                mode = probe.mode
                if probe.format != "PNG" or mode != "RGBA":
                    errors.append(event_id + ": expected PNG RGBA")
        except Exception as error:
            errors.append(event_id + ": unreadable image: " + str(error))
            continue
        guid = re.search(r"^guid: ([a-f0-9]+)", settings, re.M)[1]
        if not re.search(r"^  textureType: 8$", settings, re.M) or not re.search(r"^  spriteMode: 1$", settings, re.M):
            errors.append(event_id + ": not a single Sprite")
        art_id = "EVENT_" + event_id.upper()
        if sync and art_id not in existing:
            serialized += f"  - id: {art_id}\n    sprite: {{fileID: 21300000, guid: {guid}, type: 3}}\n"
            existing[art_id] = guid
        if existing.get(art_id) != guid:
            errors.append(event_id + ": missing/incorrect runtime Sprite reference")
        rows.append({"event_id": event_id, "path": image.relative_to(ROOT).as_posix(), "sprite_guid": guid,
                     "size": size, "mode": mode,
                     "configured_in_new_engine": event_id in configured, "legacy_asset": event_id in legacy})
    if sync:
        # The registry contains only the root serialized object and its Entry[] data.
        # Preserve every existing entry; add stable references to the verified Sprite GUIDs.
        registry.write_text(serialized, encoding="utf-8")
    report = {"date": "2026-11-25", "mode": "serialized_source_audit", "total_events": len(configured | legacy),
              "artwork_entries": len(rows), "missing": errors, "passed": not errors, "events": rows}
    out = ROOT / "docs/art/qa/event-coverage-2026-11-25.json"
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "events"}, ensure_ascii=False, indent=2))
    return not errors


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--sync", action="store_true", help="Add missing Sprite references while retaining existing registry entries")
    raise SystemExit(0 if audit(parser.parse_args().sync) else 1)
