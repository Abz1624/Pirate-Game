using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "LogbookDefinition", menuName = "HighSeas/Logbook Definition")]
    public sealed class LogbookDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public StatId primaryStat;
        public float perLevelAdditive;
        public float perLevelMultiplicative = 1f;
        public int maxLevel = 10;
        public string unlockQuestId;
    }
}
