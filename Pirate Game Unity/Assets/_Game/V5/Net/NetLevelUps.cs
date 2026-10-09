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

        /// <summary>The next client tick the server has not applied yet (where a pause starts at the earliest).</summary>
        public Func<uint> NextInputTick;
        /// <summary>V5 M9b review: the newest client input tick received (a named hold is clamped to it plus the lead).</summary>
        public Func<uint> NewestInputTick;
        /// <summary>V5 M9b review: whether a client is there to name its hold tick (null or false: the ship holds at once
        /// from the next unapplied tick, as before).</summary>
        public Func<bool> ClientCanNameHold;
        public float HoldWaitSeconds = NetProtocol.HoldWaitSeconds;

        /// <summary>The cards are up and the server waits for the client's hold tick; the ship still moves meanwhile.</summary>
        public bool AwaitingHold { get; private set; }
        float _holdDeadline;
        /// <summary>Holds named by the client; those clamped (moved into the allowed range); waits that ran out.</summary>
        public int HoldsNamed { get; private set; }
        public int HoldsClamped { get; private set; }
        public int HoldsTimedOut { get; private set; }
        /// <summary>V5 M9b review: a late pick's held ticks [from, to) are stepped now on the inputs the server has for them.</summary>
        public Action<uint, uint> CatchUp;
        /// <summary>V5 M9b review: a late hold puts the ship back to its state before this tick (kept by the server for
        /// every tick applied while it waited for the hold).</summary>
        public Action<uint> Rewind;
        public int HoldsRewound { get; private set; }
        uint _dealtAt;
        public int LatePicksCaughtUp { get; private set; }
        /// <summary>The cards were sent again after a refused pick (at most once a set).</summary>
        bool _resentAfterRefusal;
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
            _resentAfterRefusal = false;
            var next = Next();
            // A chained set (the next level-up, a refresh) or a new one before the last resume tick was reached: the hold
            // goes on from where it began. Otherwise the client names its hold tick (V5 M9b review): until then the ship
            // keeps applying its inputs, so the client takes no ticks back.
            var continuing = AwaitingHold || PauseFrom != 0 && (ResumeTick == 0 || next < ResumeTick);
            ResumeTick = 0;
            if (!continuing)
            {
                if (ClientCanNameHold != null && ClientCanNameHold())
                {
                    PauseFrom = 0;
                    AwaitingHold = true;
                    _holdDeadline = Time.realtimeSinceStartup + HoldWaitSeconds;
                    _dealtAt = next;
                }
                else PauseFrom = next;
            }
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

        uint Next() => Math.Max(1u, NextInputTick != null ? NextInputTick() : 1u);

        /// <summary>
        /// V5 M9b review: the client names the tick its ship holds from (the one after the tick it had reached when the cards
        /// arrived). Clamped to at least the next unapplied tick (the server cannot take a tick back) and at most the
        /// newest input received plus <see cref="NetProtocol.HoldLeadTicks"/> (a client cannot sail on through the pause);
        /// the inputs before it are applied as they come, then the ship holds. A clamped hold sends the cards again with
        /// the tick the server took. Null when taken; else why not (nothing changes).
        /// </summary>
        public string ApplyHold(uint requested)
        {
            if (!AwaitingHold) return "no hold awaited";
            var next = Next();
            var newest = NewestInputTick != null ? NewestInputTick() : next;
            var max = Math.Max(next, newest + NetProtocol.HoldLeadTicks);
            // A hold that arrives late (its packet resent) names a tick the server has already applied since the cards were
            // dealt: the ship goes back to its state before that tick (the run is paused, so nothing else moved), and the
            // hold stays exact. Never before the tick the cards were dealt at.
            var min = Math.Max(1u, _dealtAt);
            var hold = requested < min ? min : requested > max ? max : requested;
            if (hold < next)
            {
                Rewind?.Invoke(hold);
                HoldsRewound++;
            }
            FixHold(hold);
            HoldsNamed++;
            Debug.Log($"[V5 Net] hold named {requested}: next unapplied {next}, newest input {newest}, held from {hold}");
            if (hold != requested) { HoldsClamped++; if (Pending) Send?.Invoke(Message()); }
            return null;
        }

        void FixHold(uint hold)
        {
            PauseFrom = Math.Max(1u, hold);
            AwaitingHold = false;
        }

        /// <summary>V5 M9b review: whether a refused pick may send the cards again (once a set; later refusals send nothing).</summary>
        public bool TakeResendAfterRefusal()
        {
            if (!Pending || _resentAfterRefusal) return false;
            _resentAfterRefusal = true;
            return true;
        }

        /// <summary>The player's choice. Null when applied; else why it was refused (the cards stay up).</summary>
        public string Apply(in PickMessage pick)
        {
            if (pick.action == NetProtocol.PickAction.Hold) return ApplyHold(pick.resumeTick);
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

        /// <summary>The server's clock: no hold named in time holds from the next unapplied tick (and tells the client); an
        /// unanswered set takes the first card (the default pick).</summary>
        public void Tick()
        {
            if (AwaitingHold && Time.realtimeSinceStartup >= _holdDeadline)
            {
                FixHold(Next());
                HoldsTimedOut++;
                if (Pending) Send?.Invoke(Message());
            }
            if (!Pending || Time.realtimeSinceStartup < Deadline) return;
            TimedOut++;
            Resolve(NetProtocol.OfferResolution.TimedOut, 0, 0);
        }

        void Resolve(NetProtocol.OfferResolution how, int index, uint clientResume)
        {
            var seq = Seq;
            var offer = Offers[Mathf.Clamp(index, 0, Offers.Count - 1)];
            if (AwaitingHold) FixHold(Next()); // resolved before the client named its hold (a quick pick, a timeout)
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
            // the client's tick (a timeout: from the next tick not yet applied).
            uint resume = 0;
            if (!Pending)
            {
                var next = Next();
                const uint window = 2u * NetProtocol.HoldLeadTicks;
                const uint late = NetProtocol.LateTickWindow;
                resume = next;
                if (clientResume != 0)
                {
                    // V5 M9b review: the client's resume tick is taken up to twice the lead past the next unapplied tick (it
                    // cannot stretch the hold). A late pick (a lost packet resent) names a tick the server has already held:
                    // the inputs it has for those ticks are applied now (at most LateTickWindow back, never before the pause
                    // began), so the resume is exact on both sides.
                    var floor = Math.Max(Math.Max(1u, PauseFrom), next > late ? next - late : 1u);
                    resume = clientResume > next + window ? next + window : clientResume < floor ? floor : clientResume;
                    if (resume < next)
                    {
                        CatchUp?.Invoke(resume, next);
                        LatePicksCaughtUp++;
                    }
                }
                ResumeTick = resume;
                Debug.Log($"[V5 Net] cards {how}: held from {PauseFrom}, client resume {clientResume}, next unapplied {next}, resume {resume}");
            }
            Close(how, (byte)Mathf.Clamp(index, 0, 255), resume, seq);
        }

        void Close(NetProtocol.OfferResolution how, byte index, uint resume, ushort? seq = null)
        {
            if (how == NetProtocol.OfferResolution.Cancelled)
            {
                AwaitingHold = false;
                if (PauseFrom == 0) PauseFrom = Next();
                ResumeTick = Next();
                resume = ResumeTick;
            }
            SendClosed?.Invoke(new OfferClosedMessage { seq = seq ?? Seq, how = how, index = index, resumeTick = resume });
        }

        /// <summary>Whether the ship holds on client tick <paramref name="tick"/> (the cards' pause); a tick past the resume
        /// clears the pause.</summary>
        public bool Holds(uint tick)
        {
            if (AwaitingHold || PauseFrom == 0 || tick < PauseFrom) return false;
            if (ResumeTick == 0) return true;
            if (tick < ResumeTick) return true;
            PauseFrom = 0;
            ResumeTick = 0;
            return false;
        }
    }
}
