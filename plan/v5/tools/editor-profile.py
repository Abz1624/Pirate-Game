"""V5 phone bench, Editor side (2 Oct): runs PhoneBench in the open Editor with profile=1 for each crowd size and saves the
"[BenchProfile]" line (average and worst ms per game marker, Unity system and script) to plan/v5/perf/editor-profile/.
Usage: python plan/v5/tools/editor-profile.py 0 80 160   (EXTRA="bars=0" adds query arguments; TAG names the files)"""
import json, os, re, subprocess, sys, tempfile, time

os.environ['UNITY_PROJECT_PATH'] = os.path.abspath('Pirate Game Unity')
out = 'plan/v5/perf/editor-profile'
os.makedirs(out, exist_ok=True)


def unity(*args):
    return subprocess.run(['unity', 'command', *args], capture_output=True, text=True, encoding='utf-8', shell=True).stdout


def latest(raw, tag, phase=None):
    raw = raw.replace('\\"', '"')
    pattern = r'\[' + tag + r'\] (\{"phase":"' + (phase or '[a-z]+') + r'".*?\})(?:\n|")'
    hits = re.findall(pattern, raw)
    return json.loads(hits[-1]) if hits else None


extra = os.environ.get('EXTRA', '')
tag = os.environ.get('TAG', '')
for crowd in sys.argv[1:] or ['0', '80']:
    unity('editor_stop')
    time.sleep(3)
    arm = os.path.join(tempfile.gettempdir(), 'v5-bench-arm.cs')
    with open(arm, 'w', encoding='utf-8') as f:
        f.write(f'UnityEngine.PlayerPrefs.SetString("v5_bench_args", "bench=editor-c{crowd}&q=mid&crowd={crowd}&profile=1&short=2{"&" + extra if extra else ""}"); UnityEngine.PlayerPrefs.Save(); return 1;')
    unity('eval_file', '--file', arm)
    unity('clear_console')
    unity('open_scene', '--path', 'Assets/_Game/Scenes/V4/V4Boot.unity')
    time.sleep(2)
    unity('editor_play')
    profile = None
    for _ in range(40):
        time.sleep(5)
        raw = unity('console', '--tail', '400', '--format', 'json')
        profile = latest(raw, 'BenchProfile', 'profile')
        if profile:
            break
    result = {'crowd': int(crowd), 'profile': profile, 'early': latest(raw, 'Bench', 'early'), 'peak': latest(raw, 'Bench', 'peak')}
    with open(f'{out}/profile-c{crowd}{tag}.json', 'w', encoding='utf-8') as f:
        json.dump(result, f, indent=1)
    print(f"crowd {crowd}: {'ok' if profile else 'NO PROFILE'}; peak fps {result['peak']['fps'] if result['peak'] else '-'}")
unity('editor_stop')
