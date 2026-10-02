#!/usr/bin/env bash
# Act 2, pass 2: natural play (no clock jumps) so the crowd is real. Wall time ~2.2x game time while recording.
source "$(dirname "$0")/ue.sh"
set -u
SEED=${SEED:-202}
fresh $SEED; E "$TD.Prepare(false,true,true,\"Average\")"
shot() { echo "== $1 $(E "$TD.Status()")"; }

shot open_low2;  E "$TD.Camera(\"low\")"; take v2_open_low 14
shot open_hero;  E "$TD.Camera(\"hero\")"; take v2_open_hero 12
waitt 150
for w in lightning_mast powder_kegs flying_cutlasses; do
  shot "weapon $w"; E "$TD.Weapon(\"$w\",6)"; E "$TD.Camera(\"chase\",16)"; take "v2_weapon_$w" 14
done
waitt 205
shot horde_line; E "$TD.Camera(\"wide\",40)"; take v2_horde_line 30
shot crowd_top;  E "$TD.Camera(\"top\",30)"; take v2_crowd_top 16
waitt 222
shot peak_orbit; E "$TD.Camera(\"orbit\",20,16)"; take v2_peak_orbit 20
shot evolve; E "$TD.Weapon(\"lightning_mast\",8)"; E "$TD.Camera(\"chase\",16)"; rec v2_evolve; sleep 6; E "$TD.Evolve(\"lightning_mast\",\"tempest_mast\")"; sleep 14; recstop
shot boss; E "$TD.SummonBoss()"; sleep 1; echo "boss objects: $(E "$TD.Find(\"Crab\")")"
E "$TD.Camera(\"pair\",10,0,\"Crab\")"; take v2_boss_rise 40
shot boss_slowmo; E "$TD.SlowMo(0.3)"; take v2_boss_slowmo 24; E "$TD.SlowMo(1)"
shot boss_top; E "$TD.Camera(\"top\",30)"; take v2_boss_top 20
shot sink; E "$TD.Camera(\"chase\",18)"; rec v2_sink; sleep 2; E "$TD.Sink()"; sleep 24; recstop
echo done
