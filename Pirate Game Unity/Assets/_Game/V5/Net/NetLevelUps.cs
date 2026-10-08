using System;
using System.Collections.Generic;
using RoguePirates.V4.Progression;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b part 2b: the server run's level-up cards, played over the network. It is the run's
    /// <see cref="IOfferPresenter"/> on the server (registered in the run context, where a client's run registers its card
    /// presenter): the progression module deals the real cards (its own offer logic) and pauses the run as a voyage
    /// always does; this sends them to the player, takes the player's pick, skip or refresh back, checks it against the
    /// cards on the table and hands it to the module (which applies it, deals the next level-up or resumes the run). An
    /// unanswered set takes the first card after <see cref="TimeoutSeconds"/>. It decides nothing itself.
    /// </summary>
    public sealed class NetLevelUps : IOfferPresenter
    {
        Action<Offer> _selected;
        Action _skipped, _refreshed;

        public float TimeoutSeconds = NetProtocol.LevelUpTimeoutSeconds;
        /// <summary>The cards on the table (null when none).</summary>
        public List<Offer> Offers { get; private set; }
        public ushort Seq { get; private set; }
        public int RefreshesRemaining { get; private set; }
        public bool Pending { get; private set; }
        public float Deadline { get; private set; }

        /// <summary>The first client tick the paused run holds the ship on (0: not paused for cards).</summary>
        public uint PauseFrom { get; private set; }
        /// <summary>The client tick the ship moves again from (0 while the cards are up).</summary>
        public uint ResumeTick { get; private set; }

        public int Dealt { get; private set; }
        public int Picked { get; private set; }
        public int Skipped { get; private set; }
        public int Refreshed { get; private set; }
        public int TimedOut { get; private set; }
        /// <summary>Picks that did not match the cards on the table (wrong set, bad index, no refresh left): refused.</summary>
        public int Rejected { get; private set; }
        public string LastRejectReason { get; private set; }

        /// <summary>The next client tick the server has not applied yet (where a pause starts).</summary>
        public Func<uint> NextInputTick;
        /// <summary>Sends a card set to the player (the server's transport).</summary>
        public Action<OfferMessage> Send;
        public Action<OfferClosedMessage> SendClosed;
        /// <summary>The module's pending level-ups after this one (for the client's counter).</summary>
        public Func<int> PendingLevelUps;

        bool IOfferPresenter.TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining)
        {
            if (offers == null || offers.Count == 0) return false;
            Offers = new List<Offer>(offers);
            _selected = selected;
            _skipped = skipped;
            _refreshed = refreshed;
            RefreshesRemaining = refreshesRemaining;
            Seq++;
            Pending = true;
            Dealt++;
            Deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            if (PauseFrom == 0 || ResumeTick != 0) PauseFrom = Math.Max(1u, NextInputTick != null ? NextInputTick() : 1u);
            ResumeTick = 0;
            Send?.Invoke(Message());
            return true; // the module pauses the run (a voyage's rule for level-up cards)
        }

        void IOfferPresenter.Hide()
        {
            if (!Pending) return;
            Pending = false;
            Offers = null;
            Close(NetProtocol.OfferResolution.Cancelled, 0, 0);
        }

        /// <summary>The cards on the table as a message (sent again after a resume or a refused pick).</summary>
        public OfferMessage Message()
        {
            var m = new OfferMessage
            {
                seq = Seq,
                pauseFrom = PauseFrom,
                refreshesRemaining = (byte)Mathf.Clamp(RefreshesRemaining, 0, 255),
                pendingLevelUps = (byte)Mathf.Clamp(PendingLevelUps != null ? PendingLevelUps() : 1, 0, 255),
                timeoutSeconds = Mathf.Max(0f, Deadline - Time.realtimeSinceStartup),
            };
            if (Offers != null) m.offers.AddRange(Offers);
            return m;
        }

        /// <summary>The player's choice. Null when applied; else why it was refused (the cards stay up).</summary>
        public string Apply(in PickMessage pick)
        {
            string error = null;
            if (!Pending || Offers == null) error = "no cards up";
            else if (pick.seq != Seq) error = $"stale card set {pick.seq} (up: {Seq})";
            else if (pick.action == NetProtocol.PickAction.Pick && pick.index >= Offers.Count) error = $"no card {pick.index}";
            else if (pick.action == NetProtocol.PickAction.Refresh && RefreshesRemaining <= 0) error = "no refresh left";
            else if (pick.action != NetProtocol.PickAction.Pick && pick.action != NetProtocol.PickAction.Skip && pick.action != NetProtocol.PickAction.Refresh) error = "unknown action";
            if (error != null)
            {
                Rejected++;
                LastRejectReason = error;
                return error;
            }
            switch (pick.action)
            {
                case NetProtocol.PickAction.Pick: Picked++; Resolve(NetProtocol.OfferResolution.Picked, pick.index, pick.resumeTick); break;
                case NetProtocol.PickAction.Skip: Skipped++; Resolve(NetProtocol.OfferResolution.Skipped, 0, pick.resumeTick); break;
                default: Refreshed++; Resolve(NetProtocol.OfferResolution.Refreshed, 0, 0); break;
            }
            return null;
        }

        /// <summary>Tests: the cards up now time out at the next tick.</summary>
        public void ExpireForTest() => Deadline = Time.realtimeSinceStartup;

        /// <summary>The server's clock: an unanswered set takes the first card (the default pick).</summary>
        public void Tick()
        {
            if (!Pending || Time.realtimeSinceStartup < Deadline) return;
            TimedOut++;
            Resolve(NetProtocol.OfferResolution.TimedOut, 0, 0);
        }

        void Resolve(NetProtocol.OfferResolution how, int index, uint clientResume)
        {
            var seq = Seq;
            var offer = Offers[Mathf.Clamp(index, 0, Offers.Count - 1)];
            Pending = false;
            Offers = null;
            switch (how)
            {
                case NetProtocol.OfferResolution.Picked:
                case NetProtocol.OfferResolution.TimedOut: _selected?.Invoke(offer); break;
                case NetProtocol.OfferResolution.Skipped: _skipped?.Invoke(); break;
                case NetProtocol.OfferResolution.Refreshed: _refreshed?.Invoke(); break;
            }
            // The module dealt the next set (a chained level-up, a refresh) or resumed the run. Resumed: the ship moves from
            // the client's tick, or the next tick not yet applied if that one has passed (a late pick, a timeout).
            uint resume = 0;
            if (!Pending)
            {
                var next = NextInputTick != null ? NextInputTick() : 1u;
                resume = Math.Max(clientResume, Math.Max(1u, next));
                ResumeTick = resume;
            }
            Close(how, (byte)Mathf.Clamp(index, 0, 255), resume, seq);
        }

        void Close(NetProtocol.OfferResolution how, byte index, uint resume, ushort? seq = null)
        {
            if (how == NetProtocol.OfferResolution.Cancelled) { ResumeTick = Math.Max(1u, NextInputTick != null ? NextInputTick() : 1u); resume = ResumeTick; }
            SendClosed?.Invoke(new OfferClosedMessage { seq = seq ?? Seq, how = how, index = index, resumeTick = resume });
        }

        /// <summary>Whether the ship holds on client tick <paramref name="tick"/> (the cards' pause); a tick past the resume
        /// clears the pause.</summary>
        public bool Holds(uint tick)
        {
            if (PauseFrom == 0 || tick < PauseFrom) return false;
            if (ResumeTick == 0) return true;
            if (tick < ResumeTick) return true;
            PauseFrom = 0;
            ResumeTick = 0;
            return false;
        }
    }
}
