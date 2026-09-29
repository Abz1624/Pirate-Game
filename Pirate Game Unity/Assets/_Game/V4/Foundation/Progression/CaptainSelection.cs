using UnityEngine;

namespace RoguePirates.V4.Foundation.Progression
{
    public static class CaptainSelection
    {
        const string PrefKey = "v3_selected_captain";

        public static CaptainId Selected
        {
            get => (CaptainId)PlayerPrefs.GetInt(PrefKey, (int)CaptainId.Rook);
            set => PlayerPrefs.SetInt(PrefKey, (int)value);
        }
    }
}
