# Rebuilds ART_GUIDE.md from a Claude Docs markdown export: image placeholders become links to existing-art/ and model-renders/.
import re,os,sys
src=sys.argv[1]; s=open(src,encoding='utf-8').read()
BS=chr(92)
A={"Deck Cannons":"weapons-deck-cannons.png","Broadside Battery":"weapons-broadside-battery.png","Powder Kegs":"weapons-powder-keg.png","Flying Cutlasses":"weapons-flying-cutlasses.png","Flamethrower":"weapons-flamethrower.png","Lightning Mast":"weapons-lightning-mast.png","Long Nine":"weapons-long-nine.png","Swivel Guns":"weapons-swivel-guns.png","Harpoon Gun":"weapons-harpoon-gun.png",
"Voyage start":"shots-weapons-r7-start.jpg","Parrot Squadron":"shots-weapons-r7-parrot-squadron.jpg","Ramming Prow":"shots-weapons-r7-ramming-prow.jpg","Siren Bell":"shots-weapons-r7-siren-bell.jpg","Maelstrom Charge":"shots-weapons-r7-maelstrom-charge.jpg",
"Early voyage":"shots-enemies-01-early.jpg","Kraken elite":"shots-enemies-04-kraken.jpg","Barnacle King":"shots-enemies-06-boss.jpg",
"Common":"rarity-common.png","Uncommon":"rarity-uncommon.png","Rare":"rarity-rare.png","Epic":"rarity-epic.png","Legendary":"rarity-legendary-2.png",
"Player sail":"sails-playersail-1.png","Player flag":"sails-playersail.png","Smuggler flag":"sails-smugglerflag.png","Sail damage":"sails-sail-damage1.png",
"Coin":"hud-coin.png","Gold":"hud-gold-icon.png","Rum":"hud-rum-icon.png","Heart":"hud-heart.png","XP":"hud-xp.png","Skull":"hud-skull.png","Red skull":"hud-red-skull.png","Kills":"hud-kill-5.png","Hourglass":"hud-hour-glass-2.png","Barrel":"hud-barrel.png",
"Logo":"menu-rogue-pirates-logo.png","Button":"menu-button-yellow.png","Profile logo":"menu-profile-logo.png","Paper panel":"menu-small-paper-bg.png","Minimap elements":"menu-map-elements.png",
"Off-screen arrow":"shots-ui-offscreen-arrow.jpg","Item popup":"shots-ui-07-item-popup.jpg","HUD with Logbooks":"shots-ui-08-hud-logbooks.jpg","Level-up cards":"shots-ui-09-levelup-cards.jpg","PvP hub":"shots-ui-pvp-hub.jpg","PvP draft":"shots-ui-pvp-draft.jpg","PvP combat":"shots-ui-pvp-combat1.jpg","PvP storm":"shots-ui-pvp-storm.jpg","PvP end":"shots-ui-pvp-human-end-sunk.jpg","Leaderboard":"shots-ui-online-05-leaderboard.jpg"}
for c in ["Ember","Hank","Richie","Rook","Roxie","Sparky"]:
    l=c.lower(); A[c]=f"captains-{l}-character.png"; A[c+" sail"]=f"captains-{l}-sail.png"; A[c+" logo"]=f"captains-{l}-ship-logo.png"
R={"Shark model":"enemy-shark","Swordfish model":"enemy-swordfish","Hammerhead model":"enemy-hammerhead","Manta Ray model":"enemy-manta-ray","Kraken model":"enemy-kraken","Cannonboat model":"enemy-cannonboat","CannonboatNavy model (shared)":"enemy-cannonboat-navy","SmuggleBoat model (shared)":"enemy-smuggle-boat","FireBoat model (shared)":"enemy-fire-boat","RamShip model (shared)":"enemy-ram-ship","Crab model":"enemy-crab",
"Deck Cannons model":"weapon-deck-cannons","Cannonball":"weapon-cannonball","Fire Barrel keg":"weapon-fire-barrel","Fire Barrel":"weapon-fire-barrel","Flying Cutlasses model":"weapon-flying-cutlasses","Long Nine model":"weapon-long-nine","Mortar model":"weapon-mortar","Swivel Guns model":"weapon-swivel-guns","Harpoon model":"weapon-harpoon","SpikeRam":"weapon-spike-ram","BluntRam":"weapon-blunt-ram","Gold coin model":"pickup-gold-coin","XP crystal":"pickup-xp-crystal","Gold coin":"pickup-gold-coin","Gold barrel":"pickup-loot-barrel","XP barrel":"pickup-gem-barrel","Health barrel":"pickup-health-barrel",
"Treasure chest (77,650 triangles)":"poi-treasure-chest","SalvageChest.prefab (light alternative)":"poi-salvage-chest-light","Signal buoy":"poi-signal-buoy","Repair wreck":"poi-repair-wreck","Pirate tombstone":"poi-powder-cache-tombstone","Treasure map":"poi-treasure-map","Lighthouse":"poi-lighthouse","Fish bell":"weapon-fish-bell","Cutlass Sloop (player ship)":"player-cutlass-sloop","Stone arch":"poi-stone-arch",
"pirateship.fbx":"cand-pirateship","Wooden Boat.fbx":"cand-wooden-boat","Octopus.fbx":"cand-octopus","Eel.prefab":"cand-eel","FireSwordFish.prefab":"cand-fire-swordfish","CrabElite (Barnacle King today)":"enemy-crab-elite",
"Island_Tropical_1":"island-tropical-1","Island_Tropical_4":"island-tropical-4","Island_Tropical_7":"island-tropical-7","Island_Vulcano":"island-vulcano","Tree_01":"prop-tree-01","Rock_Large":"prop-rock-large","Cliffs_Tropical 1":"prop-cliffs-tropical-1","CoralReef_1":"prop-coral-reef-1","MermaidStatue":"prop-mermaid-statue","Shipwreck":"prop-shipwreck","Tower_Windward":"prop-tower-windward","Wooden bridge":"prop-wooden-bridge","Baral barrel":"prop-barrel","cannon.fbx":"weapon-cursed-cannon","Harpoon launcher":"weapon-harpoon-launcher"}
seen={}; miss=[]
def rep(m):
    alt=m.group(1).replace(BS+'_','_').strip(); seen[alt]=seen.get(alt,0)+1
    if alt in R: p='model-renders/'+R[alt]+'.png'
    elif alt=="Mortar": p='existing-art/'+("weapons-mortar.png" if seen[alt]==1 else "shots-weapons-r7-mortar.jpg")
    elif alt=="Reef Crab": p='model-renders/enemy-crab.png' if seen[alt]==1 else 'existing-art/shots-enemies-02-crab.jpg'
    else: p='existing-art/'+(A.get(alt) or 'items-'+re.sub(r'[^a-z0-9]+','-',alt.lower()).strip('-')+'.png')
    if not os.path.exists(p): miss.append((alt,p)); return m.group(0)
    return f'![{alt}]({p})'
s2=re.sub(r'&#91;image: (.*?)'+re.escape(BS)+r'\]',rep,s)
print('replaced',sum(seen.values()),'missing',miss,'left',s2.count('&#91;image'))
open('ART_GUIDE.md','w',encoding='utf-8',newline='\n').write(s2)
