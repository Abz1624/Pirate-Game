namespace HighSeas.V3
{
    /// <summary>Logbooks offered on Saltwind level-ups (spec v3.1 + Extra Hands / Oversized Shot).</summary>
    public static class V3LogbookCatalog
    {
        public static readonly string[] AllIds =
        {
            "gunnery_manual",
            "powder_drill",
            "navigators_charts",
            "carpenters_notes",
            "lookouts_glass",
            "quartermasters_ledger",
            "scavengers_net",
            "cartographers_folio",
            "extra_hands",
            "oversized_shot"
        };

        public static string ResolveLegacyId(string id) => id switch
        {
            "reinforced_hull" => "navigators_charts",
            "infamy_charter" => "cartographers_folio",
            "treasure_map" => "quartermasters_ledger",
            _ => id
        };

        public static bool IsSaltwindPoolId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            id = ResolveLegacyId(id);
            for (var i = 0; i < AllIds.Length; i++)
            {
                if (AllIds[i] == id) return true;
            }

            return false;
        }

        public static string GetDisplayName(string id) => ResolveLegacyId(id) switch
        {
            "gunnery_manual" => "Gunner's Manual",
            "powder_drill" => "Powdermaster's Drill",
            "navigators_charts" => "Navigator's Charts",
            "carpenters_notes" => "Carpenter's Notes",
            "lookouts_glass" => "Lookout's Glass",
            "quartermasters_ledger" => "Quartermaster's Ledger",
            "scavengers_net" => "Scavenger's Net",
            "cartographers_folio" => "Cartographer's Folio",
            "extra_hands" => "Extra Hands",
            "oversized_shot" => "Oversized Shot",
            _ => id?.Replace('_', ' ') ?? "Logbook"
        };
    }
}
