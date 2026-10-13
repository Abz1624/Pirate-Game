using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M3e (owner notes f, i): the game's heavy models get Unity's generated Mesh LODs (Unity 6.2+), so a model small
    /// on screen draws a lighter version of itself. Measured on 1 Oct 2026: one Salvage Chest model is ~77,650 triangles
    /// and a voyage shows about eleven, more than half of all triangles drawn (and again in the shadow map). Every model
    /// over <see cref="MinTriangles"/> that a game prefab uses is listed here; no new art is needed.
    /// </summary>
    public static class V5MeshLods
    {
        public const int MinTriangles = 6000;

        /// <summary>The models (measured 1 Oct 2026, triangles): chest 77,650; the three island models 202,599 / 97,083 / 75,813
        /// (used only by the Cannon_Islands prefabs, which no run spawns: listed because the scan reads every prefab); bridge 45,468;
        /// cannon pack 15 / 3 / 16 12,296 / 11,448 / 6,274; buoy 12,240; lighthouse 11,597; wreck 10,484; bell 8,650; map
        /// 8,110; tombstone 6,517.</summary>
        public static readonly string[] Models =
        {
            "Assets/Mesh/Cursed/Treasure_chest/Treasure_chest.fbx",
            "Assets/Mesh/Cursed/island_01/island_3.fbx",
            "Assets/Mesh/Cursed/island_01/island_2.fbx",
            "Assets/Mesh/Cursed/island_01/island.fbx",
            "Assets/Mesh/Cursed/wooden_bridge/wooden_bridge.fbx",
            "Assets/Stylish Cannon Pack/Cannon Pack/15.FBX",
            "Assets/Stylish Cannon Pack/Cannon Pack/3.FBX",
            "Assets/Stylish Cannon Pack/Cannon Pack/16.FBX",
            "Assets/3D Models/ocean_buoy/ocean_buoy.fbx",
            "Assets/3D Models/light_house/light_house.fbx",
            "Assets/Mesh/Cursed/repair wreck/repair_wreck.fbx",
            "Assets/Mesh/Cursed/bell/fish_bell.fbx",
            "Assets/Mesh/Cursed/Treasure map/Treasure map.fbx",
            "Assets/Mesh/Cursed/pirate_tombstone/pirate_tombstone.fbx",
            // Hackathon (13 Oct): the art team's enemy models (HackathonArtWiring), measured 13 Oct: drowned pursuer 13,020;
            // drowned harrier 11,782; needle sloop 11,613; ram raider 10,921; cutthroat skiff 10,016; shield frigate 9,952;
            // repair tender 8,343; powder barge 7,962; bomb ketch 7,366; kraken crown 7,180; gunner cog 7,044; net cutter 6,513.
            "Assets/3D Models/Enemies/ships/drowned_pursuer/drowned_pursuer.fbx",
            "Assets/3D Models/Enemies/ships/drowned_harrier/drowned_harrrier.fbx",
            "Assets/3D Models/Enemies/ships/needle_slop/needle_slop.fbx",
            "Assets/3D Models/Enemies/ships/ram_raider/ram_raider.fbx",
            "Assets/3D Models/Enemies/ships/CutthroatSkiff/CutthroatSkiff.fbx",
            "Assets/3D Models/Enemies/ships/shield_frigate/shield_frigate.fbx",
            "Assets/3D Models/Enemies/ships/REPAIR TENDER/REPAIR TENDER.fbx",
            "Assets/3D Models/Enemies/ships/powder barge/POWDER BARGE.fbx",
            "Assets/3D Models/Enemies/ships/BOMB_KETCH/BOOMB_KETCH.fbx",
            "Assets/3D Models/Enemies/Kraken_Crown/Kraken_Crown.fbx",
            "Assets/3D Models/Enemies/ships/GUNNER_COG/GUNNER_COG.fbx",
            "Assets/3D Models/Enemies/ships/net cutter/net cutter.fbx",
        };

        [MenuItem("RoguePirates/V5/M3e: generate Mesh LODs for the heavy models")]
        public static string Apply()
        {
            var changed = new List<string>();
            foreach (var path in Models)
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) { changed.Add("missing " + path); continue; }
                if (importer.generateMeshLods) continue;
                importer.generateMeshLods = true;
                importer.SaveAndReimport();
                changed.Add(path);
            }
            return changed.Count == 0 ? "all set" : string.Join("; ", changed);
        }

        /// <summary>Models over the limit that game prefabs use and that are not in <see cref="Models"/> (tests).</summary>
        public static List<string> HeavyModelsNotListed()
        {
            var listed = new HashSet<string>(Models);
            var seen = new HashSet<string>();
            var missing = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/GameObject", "Assets/_Game" }))
                foreach (var dep in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true))
                {
                    if (!dep.ToLowerInvariant().EndsWith(".fbx") || !seen.Add(dep) || listed.Contains(dep)) continue;
                    var triangles = AssetDatabase.LoadAllAssetsAtPath(dep).OfType<Mesh>().Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(s => (long)m.GetIndexCount(s) / 3));
                    if (triangles >= MinTriangles) missing.Add($"{dep} ({triangles})");
                }
            return missing;
        }
    }
}
