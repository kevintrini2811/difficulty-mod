"""Build and install Difficulty Mod. Does not start the game or run tests."""
from pathlib import Path
import hashlib
import os
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
GAME = Path('/mnt/FastGaming1/SteamLibrary/steamapps/common/PVZ Replanted')
SUPPORTED = 'e7333eaf87996fa44b4a3526986272319b7d01f7acdbcff52451652f61ceb696'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    if digest(GAME / 'GameAssembly.dll') != SUPPORTED:
        raise SystemExit('Unsupported GameAssembly.dll. Revalidate the native lifecycle before installing.')
    dotnet = ROOT.parent / '.tools/dotnet/dotnet'
    if not dotnet.exists():
        dotnet = shutil.which('dotnet')
    with (ROOT / 'build.log').open('w') as log:
        result = subprocess.run([str(dotnet), 'build', str(ROOT / 'DifficultyMod.csproj'),
                                 '-c', 'Release', '--nologo'], stdout=log, stderr=subprocess.STDOUT)
    if result.returncode:
        raise SystemExit((ROOT / 'build.log').read_text())
    source = ROOT / 'bin/Release/net6.0/DifficultyMod.dll'
    target = GAME / 'Mods/DifficultyMod.dll'
    if target.exists() and digest(target) != digest(source):
        backup = ROOT / 'backup' / digest(target)
        backup.mkdir(parents=True, exist_ok=True)
        shutil.copy2(target, backup / target.name)
    temporary = target.with_suffix('.dll.tmp')
    shutil.copy2(source, temporary)
    os.replace(temporary, target)
    if digest(target) != digest(source):
        raise SystemExit('Installed DLL checksum does not match the build.')
    config = GAME / 'UserData/DifficultyMod.cfg'
    if not config.exists():
        config.write_text('# 1 Easy, 2 Medium, 3 Normal, 4 Harder, 5 Extreme\n'
                          '[DifficultyMod]\nDefaultDifficulty = 3\n')
    records = []
    paths = [ROOT / 'DifficultyMod.csproj', *sorted((ROOT / 'src').glob('*.cs')),
             ROOT / 'tools/install.py', ROOT / 'tools/build_menu_icon.py',
             ROOT / 'assets/trophy.png', ROOT / 'assets/menu-icon.png', source]
    for path in paths:
        records.append(f'{digest(path)}  {path.relative_to(ROOT)}')
    (ROOT / 'SHA256SUMS').write_text('\n'.join(records) + '\n')
    print('Build succeeded. No tests run. Game not started.')
    print(f'Installed: {target}')
    print(f'SHA256: {digest(target)}')


if __name__ == '__main__':
    main()
