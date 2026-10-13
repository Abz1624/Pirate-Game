using System;
using System.Threading.Tasks;
using RoguePirates.V4.Online;
using UnityEngine;
using PV = RoguePirates.Hackathon.HackathonRules.PaymentVerdict;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (13 Oct, review): posts a signed payment's signature (<see cref="PendingTransfers"/>) to the server until it
    /// gives an answer, the same way for a 1v1 deposit, a market purchase and a listing's NFT transfer:
    /// tx_not_confirmed or an unreachable server waits and asks again (<see cref="HackathonRules.Verdict"/>); an accepted or
    /// final answer forgets the signature; anything else keeps it for a later "Check" (never a second payment). A transfer
    /// devnet still has not seen <see cref="HackathonRules.ExpireSeconds"/> after signing can never land and is forgotten
    /// as not taken. The delay is a seam for the EditMode tests.
    /// </summary>
    public static class PaymentFlow
    {
        public static Func<float, Task> Delay = DefaultDelay;

        static async Task DefaultDelay(float seconds) => await Awaitable.WaitForSecondsAsync(seconds);

        public static void UseDefaultDelay() => Delay = DefaultDelay;

        public sealed class Answer<T> where T : class
        {
            public T Data;
            public PV Verdict;
            public string Code;
            public string Message;
            public string Signature;
            /// <summary>The answer was final or accepted: the signature is no longer pending.</summary>
            public bool Done => Verdict == PV.Accepted || Verdict == PV.Final;
        }

        /// <summary>Posts the pending <paramref name="kind"/> / <paramref name="key"/> signature with <paramref name="post"/> (at
        /// most <paramref name="attempts"/> times while devnet has not confirmed it).</summary>
        public static async Task<Answer<T>> Post<T>(string kind, string key, Func<string, Task<ApiResult<T>>> post, Action<int> progress,
            bool nftWait = false, int attempts = 12) where T : class
        {
            var entry = PendingTransfers.Get(kind, key);
            if (entry == null) return new Answer<T> { Verdict = PV.Final, Code = "nothing_pending" };
            var answer = new Answer<T> { Signature = entry.signature, Verdict = PV.Wait };
            for (var attempt = 0; attempt < Math.Max(1, attempts); attempt++)
            {
                progress?.Invoke(attempt);
                ApiResult<T> r;
                try { r = await post(entry.signature); }
                catch (Exception e)
                {
                    Debug.LogWarning("PaymentFlow: " + e.Message);
                    r = new ApiResult<T> { Outcome = ApiOutcome.Offline, Message = e.Message };
                }
                answer.Code = r.ErrorCode;
                answer.Message = r.Message;
                answer.Verdict = HackathonRules.Verdict(r.Outcome, r.Status, r.ErrorCode, nftWait);
                if (answer.Verdict == PV.Wait && HackathonRules.Expired(r.ErrorCode, entry.AgeSeconds(PendingTransfers.Now())))
                {
                    answer.Verdict = PV.Final;
                    answer.Code = "expired";
                }
                switch (answer.Verdict)
                {
                    case PV.Accepted:
                        answer.Data = r.Data;
                        PendingTransfers.Forget(kind, key);
                        return answer;
                    case PV.Final:
                        PendingTransfers.Forget(kind, key);
                        return answer;
                    case PV.Hold:
                        PendingTransfers.Note(kind, key, r.Message ?? r.ErrorCode);
                        return answer;
                }
                PendingTransfers.Note(kind, key, r.ErrorCode ?? r.Outcome.ToString());
                if (attempt + 1 < attempts) await Delay(2.5f);
            }
            return answer;
        }
    }
}
