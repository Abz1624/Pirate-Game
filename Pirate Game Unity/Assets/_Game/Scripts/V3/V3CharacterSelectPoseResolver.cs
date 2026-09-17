using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.V3
{
    static class V3CharacterSelectPoseResolver
    {
        static readonly Dictionary<V3CaptainId, string> ResourcePaths = new()
        {
            { V3CaptainId.Rook, "CharacterPoses/Rook" },
            { V3CaptainId.Brine, "CharacterPoses/Hank" },
            { V3CaptainId.Richie, "CharacterPoses/Richie" },
            { V3CaptainId.Flint, "CharacterPoses/Sparky" },
            { V3CaptainId.Ember, "CharacterPoses/Amber" },
            { V3CaptainId.Roxie, "CharacterPoses/Roxy" }
        };

        static readonly Dictionary<V3CaptainId, Sprite> Cache = new();

        public static Sprite Resolve(V3CaptainId captain, V3CharacterCardArtSettings art)
        {
            if (art != null)
            {
                var fromSettings = art.GetMenuPose(captain);
                if (fromSettings != null)
                    return fromSettings;
            }

            if (Cache.TryGetValue(captain, out var cached) && cached != null)
                return cached;

            if (!ResourcePaths.TryGetValue(captain, out var path))
                return null;

            var loaded = Resources.Load<Sprite>(path);
            if (loaded != null)
                Cache[captain] = loaded;
            return loaded;
        }
    }
}
