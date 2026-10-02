#!/usr/bin/env bash
# Staged takes for the planned features (Arena, Hunt, Island Race, raids, AI captains, the next zones).
# Stand-in ships and cannon islands come from TrailerStage; nothing here is gameplay.
# Red water renders near-black at low angles, so red-water shots use high cameras.
source "$(dirname "$0")/ue.sh"
set -u
TS="RoguePirates.V4.DevTools.Trailer.TrailerStage"
SEED=${SEED:-303}
shot() { echo "== $1 $(E "$TD.Status()")"; }
reset() { E "$TS.Clear()"; E "$TS.Water(\"blue\")"; E "$TS.Mood(\"day\")"; }

# 1v1 Arena on the red water (Devil's Maw)
fresh $SEED; E "$TD.Prepare(false,true,true,\"Average\")"
shot arena; E "$TS.Water(\"red\")"; E "$TS.Rival(\"orbit\",15,90,\"roxie\")"; E "$TS.Duel(true,0.55)"
E "$TD.Camera(\"pair\",8,0,\"TrailerRival\")"; take a_arena_pair 18
E "$TD.Camera(\"top\",26)"; take a_arena_top 14

# Island Race: the leader's ghost alongside
reset; shot race; E "$TS.Rival(\"escort\",7,0,\"\",true,3)"
E "$TD.Camera(\"chase\",17)"; take a_race_chase 16
E "$TD.Camera(\"hero\",14)"; take a_race_hero 12

# Raid: cannon islands fire on the ship, the ship fires back
reset; shot raid; E "$TS.Tower(24,35)"; E "$TS.Tower(30,-40,1,\"Cannon_Islands/island_Cannon_2\")"; E "$TS.Duel(true,0.45)"
E "$TD.Camera(\"pair\",10,0,\"TrailerTower\")"; take a_raid_pair 18
E "$TD.Camera(\"orbit\",24,14)"; take a_raid_orbit 14

# AI captain patrolling the home waters at dusk
reset; shot ai_patrol; E "$TS.Mood(\"gravewater\")"; E "$TS.Tower(20,60,1,\"Cannon_Islands/island_Cannon_3\")"; E "$TS.Rival(\"escort\",10,180,\"richie\")"
E "$TD.Camera(\"orbit\",20,10)"; take a_ai_patrol 14

# Hunt on the green water (Gravewater): four ships, rain, the boss
reset; waitt 70; shot hunt; E "$TS.Water(\"green\")"; E "$TS.Rain(true)"
E "$TS.Rival(\"orbit\",18,0,\"ember\",false,10)"; E "$TS.Rival(\"orbit\",24,120,\"sparky\",false,-8)"; E "$TS.Rival(\"orbit\",30,240,\"richie\",false,12)"; E "$TS.Duel(true,0.4)"
E "$TD.Camera(\"wide\",38)"; take a_hunt_wide 20
E "$TD.Camera(\"orbit\",26,12)"; take a_hunt_orbit 16
E "$TD.SummonBoss()"; sleep 2; E "$TD.Camera(\"pair\",12,0,\"Crab\")"; take a_hunt_boss 24

# Teasers: Gravewater (green, a ghost galleon) and Devil's Maw (red, high)
E "$TS.Clear()"; E "$TS.Water(\"green\")"; E "$TS.Mood(\"gravewater\")"; shot gravewater
E "$TS.Rival(\"hold\",20,20,\"\",true)"; E "$TD.Camera(\"pair\",6,0,\"TrailerRival\")"; take a_gravewater 16
E "$TS.Clear()"; E "$TS.Water(\"red\")"; E "$TS.Mood(\"maw\")"; shot maw
E "$TD.Camera(\"wide\",30)"; take a_maw_wide 14
reset
echo done
