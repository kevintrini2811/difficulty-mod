"""Compose the native trophy and Plant Library's exported lawn for BloomEngine."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
LIBRARY = ROOT.parent / "plant-library"
SIZE = (512, 512)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    assets = ROOT / "assets"
    # Reuse the exact native Area1_Lawn export from Plant Library. Keep a local
    # copy so building or running this mod does not depend on PlantLibrary.dll.
    lawn_source = LIBRARY / "assets/lawn.png"
    shutil.copy2(lawn_source, assets / "lawn.png")
    with Image.open(assets / "lawn.png") as lawn, Image.open(assets / "trophy.png") as trophy:
        icon = ImageOps.fit(lawn.convert("RGBA"), SIZE, method=Image.Resampling.LANCZOS)
        icon.putalpha(255)
        foreground = ImageOps.contain(trophy.convert("RGBA"), (440, 440), method=Image.Resampling.LANCZOS)
        position = ((512 - foreground.width) // 2, 512 - foreground.height - 28)
        icon.alpha_composite(foreground, position)
        icon.save(assets / "menu-icon.png")
    proof = {
        "reference": "../plant-library/tools/export_menu_assets.py",
        "lawn_source": str(lawn_source),
        "lawn_provenance": json.loads((LIBRARY / "assets/sources.json").read_text()),
        "trophy_provenance": json.loads((ROOT / "research/trophy-source.json").read_text()),
        "size": SIZE, "foreground_bounds": [440, 440], "foreground_bottom": 28,
        "assets": {name: sha256(assets / name) for name in ("lawn.png", "trophy.png", "menu-icon.png")},
    }
    (assets / "menu-icon-source.json").write_text(json.dumps(proof, indent=2) + "\n")
    print(assets / "menu-icon.png")


if __name__ == "__main__":
    build()
