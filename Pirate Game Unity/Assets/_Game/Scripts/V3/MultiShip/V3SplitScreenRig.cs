using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    /// <summary>Two viewports: left ship (WASD), right ship (arrow keys).</summary>
    public static class V3SplitScreenRig
    {
        public static void Enable(PlayerShipController leftShip, PlayerShipController rightShip)
        {
            if (leftShip == null || rightShip == null)
                return;

            var templateCam = Camera.main;
            if (templateCam == null)
                return;

            var followTemplate = templateCam.GetComponent<FollowCamera>();
            templateCam.enabled = false;
            if (followTemplate != null)
                followTemplate.enabled = false;

            CreateFollowCamera("SplitCam_Left", leftShip.transform, new Rect(0f, 0f, 0.5f, 1f), templateCam, followTemplate);
            CreateFollowCamera("SplitCam_Right", rightShip.transform, new Rect(0.5f, 0f, 0.5f, 1f), templateCam, followTemplate);
        }

        static void CreateFollowCamera(
            string name,
            Transform target,
            Rect viewport,
            Camera template,
            FollowCamera followTemplate)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(template);
            cam.rect = viewport;
            cam.depth = template.depth + 1;
            cam.tag = "Untagged";

            var follow = go.AddComponent<FollowCamera>();
            follow.SetTarget(target, snap: true);
        }
    }
}
