"""Reapply the owner's six-team playtest CSV to canonical Unity data assets.

No runtime save is read/written. Ratings/GUIDs/numbers stay exactly as supplied;
generic source positions are retained as flavor and mapped to existing legal slots.
"""
import argparse
import csv
import json
from pathlib import Path
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'Assets/_Project/Data'
CSV_PATH = DATA / 'Teams/blacktop_42_player_roster.csv'
NAMESPACE = uuid.UUID('47db9969-d912-458b-9b64-14303b888b46')
STATS = ['Passing', 'Speed', 'Blocking', 'Agility', 'Catching', 'RunPower',
         'Carrying', 'Tackling', 'Coverage', 'DMoves', 'Swagger', 'RouteRunning']
FIELDS = ['passing', 'speed', 'blocking', 'agility', 'catching', 'runPower',
          'carrying', 'tackling', 'coverage', 'dMoves', 'swagger', 'routeRunning']
TEAMS = ['Kings', 'Vipers', 'Ravens', 'Wolves', 'Outlaws', 'Reapers']
# Retired Vipers were slot-named placeholders, so names cannot identify their aliases.
# This is migration metadata from the original seven source assignments, not ratings.
LEGACY_VIPERS = ['0456f958-9ee8-4512-a7ac-5e985056cb49', '1298724e-0132-4cf9-9c46-2f61da2d2142',
                 'ab354517-1bb7-4590-97d5-d468e263535a', '36988f22-c216-4261-9760-a272c4e53478',
                 'e0e2299e-aea9-451c-9739-4e9dc7624d09', 'bc5f4f7a-1e86-49c5-868b-1cc31018a4d0',
                 'd9590a53-cabe-4f1e-9e66-4dc7250724c5']
COLORS = {'Kings': (1, .5917661, 0), 'Vipers': (.15, .8, .3),
          'Ravens': (.44, .34, .65), 'Wolves': (.31, .55, .72),
          'Outlaws': (.75, .2, .16), 'Reapers': (.25, .61, .43)}
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'


def guid(path):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    return uuid.uuid5(NAMESPACE, str(path.relative_to(ROOT))).hex


def meta(path, folder=False, text=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    kind = 'DefaultImporter' if folder else 'TextScriptImporter' if text else 'NativeFormatImporter'
    extra = 'folderAsset: yes\n' if folder else ''
    main = '' if folder or text else '  mainObjectFileID: 11400000\n'
    target.write_text(f'fileFormatVersion: 2\nguid: {guid(path)}\n{extra}{kind}:\n'
                      f'  externalObjects: {{}}\n{main}  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def asset(path, script, name, body):
    path.parent.mkdir(parents=True, exist_ok=True)
    meta(path.parent, folder=True)
    common = ('  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n'
              '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
              '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n')
    content = (HEADER + common + f'  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n'
               f'  m_Name: {name}\n  m_EditorClassIdentifier:\n' + body)
    path.write_text('\n'.join(line.rstrip() for line in content.splitlines()) + '\n')
    meta(path)


def color(rgb):
    return '{r: %s, g: %s, b: %s, a: 1}' % rgb


def reference(path):
    return f'{{fileID: 11400000, guid: {guid(path)}, type: 2}}'


def offense_score(row, slot):
    # Highest-Passing QB first, then the existing line/skill fitness equations.
    if slot == 0:
        return row['Passing'] * 100 + row['Agility'] + row['Carrying']
    if slot in (5, 6):
        return row['Blocking'] * 3 + row['RunPower'] + row['Tackling']
    if slot == 1:
        return row['Speed'] + row['Agility'] + row['RunPower'] + row['Carrying'] * 2
    return row['Catching'] * 2 + row['Speed'] + row['RouteRunning'] + row['Agility']


def lineups(rows):
    remaining = rows[:]
    offense = {}
    for slot in (0, 5, 6, 1, 2, 3, 4):
        best = max(remaining, key=lambda row: offense_score(row, slot))
        offense[best['Guid']] = slot
        remaining.remove(best)
    remaining = rows[:]
    defense = {}
    # Resolve every defensive slot separately; no offense/defense pairing is assumed.
    for slot, role in ((6, 'S'), (4, 'CB'), (5, 'CB'), (2, 'LB'), (3, 'LB'), (0, None), (1, None)):
        def score(row):
            hint = 1000 if role is not None and row['Defense'] == role else 0
            value = (row['Coverage'] * 2 + row['Speed'] + row['Agility'] + row['Tackling']) if slot >= 4 else (
                row['Tackling'] * 2 + row['Speed'] + row['Coverage'] + row['DMoves'] if slot >= 2 else
                row['DMoves'] * 2 + row['Tackling'] * 2 + row['Speed'])
            return hint + value
        best = max(remaining, key=score)
        defense[best['Guid']] = slot
        remaining.remove(best)
    return [(row, offense[row['Guid']], defense[row['Guid']]) for row in sorted(rows, key=lambda r: offense[r['Guid']])]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--csv', type=Path, default=CSV_PATH)
    args = parser.parse_args()
    with args.csv.open(newline='', encoding='utf-8-sig') as stream:
        reader = csv.DictReader(stream)
        assert reader.fieldnames == ['Team', 'Guid', 'Name', 'Jersey', 'Offense', 'Defense'] + STATS, 'Unexpected CSV schema'
        rows = list(reader)
    assert len(rows) == 42 and {r['Team'] for r in rows} == set(TEAMS), 'Expected the supplied 42-player / six-team source'
    assert len({str(uuid.UUID(r['Guid'])) for r in rows}) == 42, 'Player GUIDs must be unique'
    for row in rows:
        assert row['Name'].strip() and row['Offense'] in {'QB', 'RB', 'WR', 'TE'} and row['Defense'] in {'S', 'CB', 'LB'}
        row['Jersey'] = int(row['Jersey'])
        assert row['Jersey'] >= 0
        for name in STATS:
            row[name] = int(row[name]); assert 0 <= row[name] <= 20, (row['Name'], name)
    for team in TEAMS:
        members = [r for r in rows if r['Team'] == team]
        assert len(members) == 7 and len({r['Jersey'] for r in members}) == 7, team

    # Validate the entire packet before creating/replacing any source asset.
    if args.csv.resolve() != CSV_PATH.resolve():
        shutil.copyfile(args.csv, CSV_PATH)
    meta(CSV_PATH, text=True)
    roster_script = guid(ROOT / 'Assets/_Project/Scripts/Player/TeamRoster.cs')
    identity_script = guid(ROOT / 'Assets/_Project/Scripts/Core/TeamIdentity.cs')
    team_script = guid(ROOT / 'Assets/_Project/Scripts/FrontEnd/TeamDefinition.cs')
    curve = reference(DATA / 'AttributeCurves.asset')
    definitions = []
    report = ['# Supplied playtest roster', '', 'Source: Assets/_Project/Data/Teams/blacktop_42_player_roster.csv.', '',
              'All GUIDs, names, jersey numbers and twelve ratings are imported verbatim. ',
              'Source QB/RB/WR/TE and S/CB/LB roles remain in archetypeLabel as preferences. ',
              'The starting QB uses the highest Passing rating; the two line slots take the best remaining blockers, ',
              'then RB and receivers use the existing lineup fitness equations. Defense is assigned independently ',
              'using the supplied role preferences and existing defensive fitness. No rating or curve is retuned.', '',
              'Reapply from the repository root with `python3 SourceAssets/Rosters/import_roster.py`. ',
              'The script uses the committed CSV by default, preserves existing asset GUIDs and appearance references, ',
              'and never opens runtime saves. To import a replacement source, pass `--csv /path/to/source.csv`. ',
              'For a future intentional catalog update, advance FrontEndSettings.rosterRevision after source authoring; ',
              'the importer preserves a revision greater than 1 rather than downgrading it.', '',
              '| Team | Player | Offense slot | Defense slot |', '|---|---|---|---|']
    for team in TEAMS:
        members = [r for r in rows if r['Team'] == team]
        folder = DATA / 'Teams' / team
        roster = folder / f'Roster_{team}.asset'
        identity = folder / f'Identity_{team}.asset'
        definition = DATA / 'FrontEnd' / f'{team}.asset'
        previous = {}
        appearances = {}
        if roster.exists():
            for chunk in roster.read_text().split('  - id: ')[1:]:
                old = chunk.splitlines()[0].strip()
                name = re.search(r'^    displayName: (.*)$', chunk, re.M)[1].strip().strip('"')
                alias = re.search(r'^    previousId: *(.*)$', chunk, re.M)
                previous[name] = (old, alias[1].strip() if alias else '')
                appearance = re.search(r'^    appearance: (.*)$', chunk, re.M)
                appearances[old] = appearance[1] if appearance else '{fileID: 0}'
        assignments = lineups(members)
        offense_by_id = {row['Guid']: offense for row, offense, _ in assignments}
        body = f'  teamName: {team}\n  curves: {curve}\n  players:\n'
        for row in members:
            old, alias = previous.get(row['Name'], ('', ''))
            alias = alias if old == row['Guid'] else old
            if not alias and team == 'Vipers':
                alias = LEGACY_VIPERS[offense_by_id[row['Guid']]]
            body += f"  - id: {row['Guid']}\n    previousId: {alias}\n    displayName: {json.dumps(row['Name'])}\n"
            body += f"    jerseyNumber: {row['Jersey']}\n    archetypeLabel: {json.dumps(row['Offense'] + ' / ' + row['Defense'])}\n"
            appearance = appearances.get(row['Guid'], appearances.get(old, '{fileID: 0}'))
            body += f'    accentColor: {color(COLORS[team])}\n    appearance: {appearance}\n'
            for source, field in zip(STATS, FIELDS):
                body += f'    {field}: {row[source]}\n'
        asset(roster, roster_script, f'Roster_{team}', body)
        if not identity.exists():
            asset(identity, identity_script, f'Identity_{team}', f'  teamName: {team}\n  accentColor: {color(COLORS[team])}\n'
                  '  secondaryAccentColor: {r: 0.88, g: 0.88, b: 0.86, a: 1}\n  logo: {fileID: 0}\n')
        body = f'  id: {team.lower()}\n  identity: {reference(identity)}\n  roster: {reference(roster)}\n  activeLineup:\n'
        for row, offense, defense in assignments:
            body += f"  - playerId: {row['Guid']}\n    offense: {offense}\n    defense: {defense}\n"
            report.append(f"| {team} | #{row['Jersey']} {row['Name']} | {['QB','RB','WR1','WR2','WR3','OL1','OL2'][offense]} | {['EDGE-L','EDGE-R','LB-L','LB-R','DB-L','DB-R','S'][defense]} |")
        asset(definition, team_script, team, body)
        definitions.append(reference(definition))
    settings = DATA / 'FrontEnd/FrontEndSettings.asset'
    content = settings.read_text()
    current_revision = re.search(r'^  rosterRevision: (\d+)$', content, re.M)
    revision = max(1, int(current_revision[1]) if current_revision else 0)
    content = re.sub(r'^  rosterRevision:.*\n', '', content, flags=re.M)
    content = re.sub(r'  teams:\n.*?(?=  fields:)', f'  rosterRevision: {revision}\n  teams:\n' + ''.join(f'  - {ref}\n' for ref in definitions), content, flags=re.S)
    settings.write_text(content)
    (ROOT / 'SourceAssets/Rosters/README.md').write_text('\n'.join(line.rstrip() for line in report) + '\n')
    print('Imported 42 exact records, six teams, seven independent both-ways assignments per team.')


if __name__ == '__main__':
    main()
