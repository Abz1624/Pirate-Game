using System;
using RoguePirates.V4.Config;
using RoguePirates.V4.Views;

namespace RoguePirates.V4.Progression
{
    /// <summary>The HUD's level, XP and Logbooks from the progression module (M9; was LegacyProgressionView over LevelSystem).</summary>
    public sealed class ProgressionView : IProgressionView
    {
        readonly ProgressionModule _levels;
        readonly Func<string, LogbookConfig> _logbook;

        public ProgressionView(ProgressionModule levels, Func<string, LogbookConfig> logbook)
        {
            _levels = levels;
            _logbook = logbook;
        }

        public bool Exists => _levels != null;
        public int Level => _levels.Level;
        public float CurrentXp => _levels.CurrentXp;
        public float XpToNext => _levels.XpToNext;
        public int LogbookCount => _levels.OwnedLogbooks.Count;
        public string LogbookId(int index) => _levels.OwnedLogbooks[index];

        public string LogbookName(int index)
        {
            var id = LogbookId(index);
            var config = _logbook(id);
            return config != null && !string.IsNullOrEmpty(config.displayName) ? config.displayName : id;
        }

        public int LogbookRank(int index) => _levels.LogbookLevel(LogbookId(index));
    }
}
