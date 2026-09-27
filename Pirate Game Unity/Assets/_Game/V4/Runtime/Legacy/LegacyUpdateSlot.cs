using System;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Runs a V4 tick at a legacy system's place in the frame. Until the enemy world, weapons and run clock are V4,
    /// V4 spawn logic has to run where the V3 component it replaces ran, or traces stop matching V3. Unity's update
    /// order depends on scene file IDs and on how and when components are created (measured with the profiler in play
    /// sessions: scene components in descending file-ID order; the new GameObject and the component added to the ship
    /// before them; components added to /Systems at runtime after them). So each slot is made the way V3 made its
    /// component: the ambient slot is a scene component with the file ID next to EncounterSpawner's
    /// (V4SceneSetup.ApplyM3), the others are created at the same points of the run start. Unity's test runner orders
    /// scene components differently; exact trace parity in the player is the proof of the order.
    /// </summary>
    [DeletedInPhase(14, "V4 owns the frame once the run clock is V4")]
    [AddComponentMenu("")]
    public sealed class LegacyUpdateSlot : MonoBehaviour
    {
        [Tooltip("Which V4 tick this slot runs (ambient, overtime, storm, beats, storm-edge).")]
        public string role = string.Empty;

        public Action<float> Tick;

        /// <summary>The run clock this slot saw at its last tick, and the frame (tests check the frame order with it).</summary>
        public float ClockAtTick { get; private set; } = -1f;
        public int FrameAtTick { get; private set; } = -1;

        void Update()
        {
            ClockAtTick = HighSeas.Core.RunManager.Instance != null ? HighSeas.Core.RunManager.Instance.ElapsedSeconds : -1f;
            FrameAtTick = Time.frameCount;
            Tick?.Invoke(Time.deltaTime);
        }
    }
}
