#!/usr/bin/env bash
# Re-takes for the planned shots that failed (rain renders magenta in URP, so no rain; pauses between commands).
source "$(dirname "$0")/ue.sh"
set -u
TS="RoguePirates.V4.DevTools.Trailer.TrailerStage"
S() { E "$1"; sleep 1.5; }
reset() { S "$TS.Clear()"; S "$TS.Water(\"blue\")"; S "$TS.Mood(\"day\")"; }

fresh 404; S "$TD.Prepare(false,true,true,\"Average\")"
echo "== arena top"; S "$TS.Water(\"red\")"; S "$TS.Rival(\"orbit\",15,90,\"roxie\")"; S "$TS.Duel(true,0.55)"; S "$TD.Camera(\"top\",26)"; take2 a_arena_top 14
echo "== race hero"; reset; S "$TS.Rival(\"escort\",7,0,\"\",true,3)"; S "$TD.Camera(\"hero\",14)"; take2 a_race_hero 12
echo "== raid pair"; reset; S "$TD.Prepare(false,true,false)"; S "$TS.Tower(22,35)"; S "$TS.Tower(28,-40,1,\"Cannon_Islands/island_Cannon_2\")"; S "$TS.Duel(true,0.45)"; S "$TD.Camera(\"pair\",10,0,\"TrailerTower\")"; take2 a_raid_pair 16
echo "== ai patrol"; reset; S "$TS.Mood(\"gravewater\")"; S "$TS.Tower(20,60,1,\"Cannon_Islands/island_Cannon_3\")"; S "$TS.Rival(\"escort\",10,180,\"richie\")"; S "$TD.Camera(\"orbit\",20,10)"; take2 a_ai_patrol 14
echo "== hunt"; reset; S "$TD.Prepare(false,true,true,\"Average\")"; waitt 90; S "$TS.Water(\"green\")"
S "$TS.Rival(\"orbit\",16,0,\"ember\",false,10)"; S "$TS.Rival(\"orbit\",22,120,\"sparky\",false,-8)"; S "$TS.Rival(\"orbit\",28,240,\"richie\",false,12)"; S "$TS.Duel(true,0.4)"
S "$TD.Camera(\"wide\",36)"; take2 a_hunt_wide 18
S "$TD.Camera(\"orbit\",24,12)"; take2 a_hunt_orbit 14
S "$TD.SummonBoss()"; sleep 3; S "$TD.Camera(\"pair\",12,0,\"Crab\")"; take2 a_hunt_boss 24
reset
echo done
