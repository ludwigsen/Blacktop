# Supplied playtest roster

Source: Assets/_Project/Data/Teams/blacktop_42_player_roster.csv.

All GUIDs, names, jersey numbers and twelve ratings are imported verbatim.
Source QB/RB/WR/TE and S/CB/LB roles remain in archetypeLabel as preferences.
The starting QB uses the highest Passing rating; the two line slots take the best remaining blockers,
then RB and receivers use the existing lineup fitness equations. Defense is assigned independently
using the supplied role preferences and existing defensive fitness. No rating or curve is retuned.

Reapply from the repository root with `python3 SourceAssets/Rosters/import_roster.py`.
The script uses the committed CSV by default, preserves existing asset GUIDs and appearance references,
and never opens runtime saves. To import a replacement source, pass `--csv /path/to/source.csv`.
For a future intentional catalog update, advance FrontEndSettings.rosterRevision after source authoring;
the importer preserves a revision greater than 1 rather than downgrading it.

| Team | Player | Offense slot | Defense slot |
|---|---|---|---|
| Kings | #7 Marcus Reed | QB | EDGE-R |
| Kings | #34 Andre Mitchell | RB | LB-L |
| Kings | #21 Jalen Brooks | WR1 | DB-L |
| Kings | #88 Darius Cole | WR2 | S |
| Kings | #11 Devon Hayes | WR3 | DB-R |
| Kings | #55 Malik Turner | OL1 | LB-R |
| Kings | #42 Isaiah Grant | OL2 | EDGE-L |
| Vipers | #8 Jordan Bennett | QB | EDGE-R |
| Vipers | #22 Xavier Foster | RB | LB-L |
| Vipers | #31 Nate Sullivan | WR1 | DB-L |
| Vipers | #17 Cameron Price | WR2 | S |
| Vipers | #90 Terrence Walker | WR3 | DB-R |
| Vipers | #50 Chris Dalton | OL1 | LB-R |
| Vipers | #63 DeShawn Carter | OL2 | EDGE-L |
| Ravens | #6 Evan Mercer | QB | EDGE-R |
| Ravens | #72 Colin Hayes | RB | DB-R |
| Ravens | #13 Kendrick Shaw | WR1 | DB-L |
| Ravens | #58 Damon Wright | WR2 | EDGE-L |
| Ravens | #91 Reggie Foster | WR3 | S |
| Ravens | #44 Trevor Banks | OL1 | LB-R |
| Ravens | #27 Raymond Ellis | OL2 | LB-L |
| Wolves | #2 Malcolm Pierce | QB | EDGE-R |
| Wolves | #24 Brandon Fields | RB | EDGE-L |
| Wolves | #9 Tyrell Morgan | WR1 | DB-L |
| Wolves | #47 Leonard Price | WR2 | S |
| Wolves | #83 Andre Wallace | WR3 | DB-R |
| Wolves | #35 Curtis Vaughn | OL1 | LB-L |
| Wolves | #66 Marcus Holloway | OL2 | LB-R |
| Outlaws | #5 Caleb Mercer | QB | EDGE-R |
| Outlaws | #68 Trey Dalton | RB | DB-R |
| Outlaws | #12 Dante Holloway | WR1 | DB-L |
| Outlaws | #54 Jamal Cross | WR2 | S |
| Outlaws | #87 Isaiah Boone | WR3 | EDGE-L |
| Outlaws | #39 Marcus Vaughn | OL1 | LB-R |
| Outlaws | #26 Rico Bennett | OL2 | LB-L |
| Reapers | #3 Devon Cross | QB | EDGE-R |
| Reapers | #74 Cameron Blake | RB | DB-R |
| Reapers | #16 Kareem Ellis | WR1 | DB-L |
| Reapers | #57 Jaylen Frost | WR2 | S |
| Reapers | #96 Malik Stone | WR3 | EDGE-L |
| Reapers | #45 Derrick Mason | OL1 | LB-R |
| Reapers | #28 Tyson Grant | OL2 | LB-L |
