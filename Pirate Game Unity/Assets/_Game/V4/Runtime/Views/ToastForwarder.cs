using System.Collections.Generic;

namespace RoguePirates.V4.Views
{
    /// <summary>
    /// The run's one toast sink (M14; was V3ToastHud's static instance, called from each legacy adapter). Runtime modules
    /// and the run lifecycle show toasts through it; the presentation root plugs the toast presenter in when it binds.
    /// Toasts shown before that (run start) are queued, up to <see cref="MaxPending"/>, and shown in order once the
    /// presenter arrives. A destroyed presenter counts as none.
    /// </summary>
    public sealed class ToastForwarder : IToastSink, RoguePirates.V4.Stage.IStageToasts
    {
        public const int MaxPending = 8;

        IToastSink _target;
        readonly Queue<(string message, float seconds)> _pending = new Queue<(string, float)>();

        public int PendingCount => _pending.Count;

        IToastSink Target
        {
            get
            {
                if (_target is UnityEngine.Object unityObject && unityObject == null) _target = null;
                return _target;
            }
        }

        public void Show(string message, float seconds)
        {
            if (string.IsNullOrEmpty(message)) return;
            var target = Target;
            if (target != null) { target.Show(message, seconds); return; }
            if (_pending.Count >= MaxPending) _pending.Dequeue();
            _pending.Enqueue((message, seconds));
        }

        /// <summary>The presenter that draws toasts (null detaches it).</summary>
        public void SetTarget(IToastSink target)
        {
            _target = target;
            var live = Target;
            if (live == null) return;
            while (_pending.Count > 0)
            {
                var (message, seconds) = _pending.Dequeue();
                live.Show(message, seconds);
            }
        }
    }
}
