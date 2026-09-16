namespace HighSeas.V3
{
    /// <summary>Logbooks that exist in GameDatabase / level-up offers (stat growth, not generic +X% damage items).</summary>
    public static class V3LogbookCatalog
    {
        public static readonly string[] AllIds =
        {
            "gunnery_manual",
            "powder_drill",
            "oversized_shot",
            "extra_hands",
            "reinforced_hull",
            "carpenters_notes",
            "infamy_charter",
            "treasure_map"
        };

        public static string GetDisplayName(string id) => id switch
        {
            "gunnery_manual" => "Gunnery Manual",
            "powder_drill" => "Powder Drill",
            "oversized_shot" => "Oversized Shot Chart",
            "extra_hands" => "Extra Hands Ledger",
            "reinforced_hull" => "Reinforced Hull Plans",
            "carpenters_notes" => "Carpenter's Notes",
            "infamy_charter" => "Infamy Charter",
            "treasure_map" => "Treasure Map Folio",
            _ => id?.Replace('_', ' ') ?? "Logbook"
        };
    }
}
