using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    [CreateAssetMenu(fileName = "LogbookDefinition", menuName = "RoguePirates/Logbook Definition")]
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
