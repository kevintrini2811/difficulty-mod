"""Export the existing native cup without launching Unity or editing its pixels."""
from pathlib import Path
import hashlib
import json
import UnityPy

ROOT = Path(__file__).resolve().parents[1]
GAME = Path('/mnt/FastGaming1/SteamLibrary/steamapps/common/PVZ Replanted')
BUNDLES = GAME / 'Replanted_Data/StreamingAssets/aa/StandaloneWindows64'
environment = UnityPy.Environment()
inputs = [next((BUNDLES / 'defaultlocalgroup_assets_ui').glob('sprites*.bundle')),
          BUNDLES / 'uiatlasshared_assets_all.bundle']
for path in inputs:
    environment.load_file(str(path))
for obj in environment.objects:
    if obj.type.name != 'Sprite' or obj.path_id != -8573436934360524815:
        continue
    sprite = obj.read()
    assert sprite.m_Name == 'SPR_Challenges_Trophy'
    output = ROOT / 'assets/trophy.png'
    output.parent.mkdir(exist_ok=True)
    sprite.image.save(output)
    metadata = {'sprite': sprite.m_Name, 'path_id': obj.path_id,
                'output_sha256': hashlib.sha256(output.read_bytes()).hexdigest(),
                'inputs': {str(path.relative_to(GAME)): hashlib.sha256(path.read_bytes()).hexdigest()
                           for path in inputs}}
    (ROOT / 'research/trophy-source.json').write_text(json.dumps(metadata, indent=2) + '\n')
    print(output)
    break
else:
    raise SystemExit('Expected native cup sprite not found; no asset written.')
