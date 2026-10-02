#!/usr/bin/env bash
# Act 2 (The Voyage) takes. Run from the repo root with the Editor open on V4SaltwindRun in Play mode.
# Wall time is ~2-2.5x game time in the Editor; the Recorder keeps game time at a fixed 60 fps.
source "$(dirname "$0")/ue.sh"
set -u
SEED=${SEED:-101}

shot() { echo "== $1"; }

shot open_low;   fresh $SEED; E "$TD.Prepare(false,true,true,\"Strong\")"; E "$TD.Camera(\"low\")"; take v_open_low 12
shot open_crane; fresh $SEED; E "$TD.Prepare(false,true,true,\"Strong\")"; E "$TD.Camera(\"crane\",0,4)"; take v_open_crane 14
shot hero_sail;  E "$TD.Camera(\"hero\")"; take v_hero_sail 12

# Weapon montage: a crowd from the clock, one weapon each, ranked up.
fresh $SEED; E "$TD.Prepare(false,true,true,\"Strong\")"; E "$TD.JumpTo(190)"; sleep 8
i=0
for w in lightning_mast powder_kegs flying_cutlasses broadside_battery mortar long_nine; do
  cams=(chase hero top chase hero top); cam=${cams[$i]}
  shot "weapon $w ($cam)"; E "$TD.Loadout(\"$w\")"; E "$TD.Weapon(\"$w\",6)"; E "$TD.Camera(\"$cam\")"; sleep 3; take "v_weapon_$w" 12
  i=$((i+1))
done

shot evolve; E "$TD.Loadout(\"lightning_mast\")"; E "$TD.Weapon(\"lightning_mast\",6)"; E "$TD.Camera(\"chase\")"; sleep 2
rec v_evolve; sleep 6; E "$TD.Evolve(\"lightning_mast\",\"tempest_mast\")"; sleep 10; recstop

shot horde_line; E "$TD.Loadout(\"lightning_mast,flying_cutlasses,powder_kegs\")"; E "$TD.JumpTo(208)"; E "$TD.Camera(\"wide\")"; take v_horde_line 22
shot horde_ring; E "$TD.JumpTo(553)"; E "$TD.Camera(\"wide\",45)"; take v_horde_ring 22

shot boss_rise; E "$TD.SummonBoss()"; E "$TD.Camera(\"orbit\",22,14)"; take v_boss_rise 36
shot boss_fight; E "$TD.Camera(\"top\",26)"; take v_boss_fight 20
shot boss_slowmo; E "$TD.SlowMo(0.3)"; take v_boss_slowmo 20; E "$TD.SlowMo(1)"
shot sink; E "$TD.Camera(\"chase\",16)"; rec v_sink; sleep 3; E "$TD.Sink()"; sleep 14; recstop
echo done
