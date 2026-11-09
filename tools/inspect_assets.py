"""Inspect native UI assets offline; never starts Unity or the game."""
from pathlib import Path
import json
import UnityPy

ROOT = Path(__file__).resolve().parents[1]
GAME = Path('/mnt/FastGaming1/SteamLibrary/steamapps/common/PVZ Replanted')
BUNDLES = GAME / 'Replanted_Data/StreamingAssets/aa/StandaloneWindows64'
for pattern in ('uipanels*.bundle', 'defaultlocalgroup_scenes_frontend*.bundle', 'uiatlasfrontend*.bundle', 'uiatlasshared*.bundle'):
    for path in BUNDLES.glob(pattern):
        env = UnityPy.load(str(path))
        print(path.name)
        for key, value in env.container.items():
            print('ASSET', key, value.path_id)
        records = {}
        for obj in env.objects:
            if obj.type.name in ('GameObject', 'RectTransform', 'MonoBehaviour', 'MonoScript', 'Sprite'):
                try:
                    tree = obj.read_typetree()
                    records[obj.path_id] = {'type': obj.type.name, 'data': tree}
                    name = tree.get('m_Name', '')
                    if any(word in name.lower() for word in ('troph', 'mini', 'level', 'challenge')):
                        print(obj.type.name, obj.path_id, name)
                except Exception as error:
                    print('ERROR', obj.path_id, str(error))
        (ROOT / 'research' / (path.stem + '.json')).write_text(json.dumps(records, indent=2, default=lambda value: '<binary>'))
