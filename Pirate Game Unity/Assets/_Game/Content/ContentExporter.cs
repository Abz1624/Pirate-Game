using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace RoguePirates.Content
{
    public sealed class ContentManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public string ContentVersion { get; set; } = "baseline_v1";
        public string ExportedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
        public List<ContentEntry> Entries { get; set; } = new();
        public string ManifestHash { get; set; } = string.Empty;
    }

    public sealed class ContentEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ReleasePhase { get; set; } = "M1";
        public List<string> AllowedModes { get; set; } = new() { "voyage" };
        public Dictionary<string, float> NumericFields { get; set; } = new();
    }

    public static class ContentExporter
    {
        public static ContentManifest ExportBaseline()
        {
            var manifest = new ContentManifest
            {
                Entries =
                {
                    new ContentEntry
                    {
                        Id = "weapon_deck_cannons",
                        Type = "Weapon",
                        ReleasePhase = "M1",
                        NumericFields = new Dictionary<string, float>
                        {
                            ["baseDamage"] = 12f,
                            ["cooldownSeconds"] = 0.85f
                        }
                    },
                    new ContentEntry
                    {
                        Id = "enemy_shark",
                        Type = "Enemy",
                        ReleasePhase = "M1",
                        NumericFields = new Dictionary<string, float>
                        {
                            ["baseHp"] = 35f,
                            ["baseDamage"] = 8f,
                            ["moveSpeed"] = 4.5f
                        }
                    },
                    new ContentEntry
                    {
                        Id = "enemy_barnacle_king",
                        Type = "Boss",
                        ReleasePhase = "M2",
                        NumericFields = new Dictionary<string, float>
                        {
                            ["baseHp"] = 500f,
                            ["baseDamage"] = 20f
                        }
                    }
                }
            };

            manifest.ManifestHash = ComputeHash(manifest);
            return manifest;
        }

        public static string ToJson(ContentManifest manifest)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"schemaVersion\":").Append(manifest.SchemaVersion).Append(',');
            sb.Append("\"contentVersion\":\"").Append(manifest.ContentVersion).Append("\",");
            sb.Append("\"manifestHash\":\"").Append(manifest.ManifestHash).Append("\",");
            sb.Append("\"entries\":[");
            for (var i = 0; i < manifest.Entries.Count; i++)
            {
                var e = manifest.Entries[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(e.Id).Append("\",\"type\":\"").Append(e.Type).Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string ComputeHash(ContentManifest manifest)
        {
            var sb = new StringBuilder();
            sb.Append(manifest.SchemaVersion).Append('|').Append(manifest.ContentVersion).Append('|');
            foreach (var e in manifest.Entries)
                sb.Append(e.Id).Append(':').Append(e.Type).Append(';');
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
    }
}
