using UnityEngine;

namespace HighSeas.V3
{
    public static class V3CaptainSelection
    {
        const string PrefKey = "v3_selected_captain";

        public static V3CaptainId Selected
        {
            get => (V3CaptainId)PlayerPrefs.GetInt(PrefKey, (int)V3CaptainId.Rook);
            set => PlayerPrefs.SetInt(PrefKey, (int)value);
        }
    }
}
