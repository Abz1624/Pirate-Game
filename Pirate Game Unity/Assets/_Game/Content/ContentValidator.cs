using System.Collections.Generic;

namespace RoguePirates.Content
{
    public sealed class ContentValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    public static class ContentValidator
    {
        public static ContentValidationResult Validate(ContentManifest manifest)
        {
            var result = new ContentValidationResult();
            if (manifest.SchemaVersion < 1)
                result.Errors.Add("schemaVersion must be >= 1");

            if (string.IsNullOrWhiteSpace(manifest.ContentVersion))
                result.Errors.Add("contentVersion is required");

            var ids = new HashSet<string>();
            foreach (var entry in manifest.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    result.Errors.Add("entry missing id");
                    continue;
                }

                if (!ids.Add(entry.Id))
                    result.Errors.Add($"duplicate id: {entry.Id}");

                if (entry.AllowedModes == null || entry.AllowedModes.Count == 0)
                    result.Warnings.Add($"{entry.Id}: no allowedModes specified");
            }

            if (manifest.Entries.Count == 0)
                result.Warnings.Add("manifest has zero entries");

            return result;
        }
    }
}
