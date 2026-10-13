using System;
using System.Threading.Tasks;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): the island, raid and marketplace API calls (server/README.md, "Hackathon: islands, RUM,
    /// raids, island NFT market") with the wallet profile's session, and the wallet steps around them (purchase
    /// payment, NFT to escrow). Every call returns a player-facing error (or null) instead of throwing.
    /// </summary>
    public static class IslandApi
    {
        static ApiClient Client => OnlineService.Client;
        static string Token => OnlineService.Active != null ? OnlineService.Active.token : null;

        /// <summary>The escrow (treasury) address, cached from GET /api/market.</summary>
        public static string Treasury { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Treasury = null;

        // ---------------------------------------------------------------- islands

        public static async Task<(IslandWorld world, string error)> World()
        {
            await OnlineService.EnsureConnected();
            var r = await Client.Get<IslandWorld>("/api/islands", Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("world map", r));
        }

        public static async Task<(MyIslands mine, string error)> Mine()
        {
            if (Token == null) return (null, "Sign in with a wallet first (Wallet button, top of the menu).");
            var r = await Client.Get<MyIslands>("/api/islands/mine", Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("islands", r));
        }

        public static async Task<(IslandView island, string error)> Claim(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length < 3 || name.Length > 16) return (null, "Island names are 3 to 16 characters.");
            if (Token == null) return (null, "Sign in with a wallet first (Wallet button, top of the menu).");
            var r = await Client.Post<IslandView>("/api/islands/claim", new ClaimRequest { name = name }, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("claim", r));
        }

        public static async Task<(IslandView island, string error)> RetryMint(string islandId)
        {
            var r = await Client.Post<IslandView>("/api/islands/" + islandId + "/mint", null, Token);
            if (r.ErrorCode == "already_minted") return (null, null);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("mint", r));
        }

        public static async Task<(CollectResponse data, string error)> Collect(string islandId)
        {
            var r = await Client.Post<CollectResponse>("/api/islands/" + islandId + "/collect", null, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("collect", r));
        }

        public static async Task<(BuildResponse data, string error)> Build(string islandId, string kind)
        {
            var r = await Client.Post<BuildResponse>("/api/islands/" + islandId + "/build", new BuildRequest { kind = kind }, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("upgrade", r));
        }

        // ---------------------------------------------------------------- raids

        /// <summary>Starts a raid (pays the 25 RUM fee). Raids have no SOL stake (SOL wagers are only in 1v1).</summary>
        public static async Task<(RaidStart raid, string error)> StartRaid(string islandId)
        {
            if (Token == null) return (null, "Sign in with a wallet first (Wallet button, top of the menu).");
            var body = new RawJsonBody("{\"islandId\":\"" + islandId + "\"}");
            var r = await Client.Post<RaidStart>("/api/raids/start", body, Token);
            return r.Ok && r.Data != null && !string.IsNullOrEmpty(r.Data.raidId) ? (r.Data, null) : (null, Error("raid", r));
        }

        /// <summary>A practice raid on your own island with its saved design (owner, 12 Oct): no fee, no loot, no shield, no
        /// raid record and nothing to report afterwards.</summary>
        public static async Task<(RaidStart raid, string error)> PracticeRaid(string islandId)
        {
            if (Token == null) return (null, "Sign in with a wallet first (Wallet button, top of the menu).");
            var r = await Client.Post<RaidStart>("/api/raids/practice", new PracticeRequest { islandId = islandId }, Token);
            if (!r.Ok || r.Data == null) return (null, Error("practice raid", r));
            r.Data.practice = true;
            return (r.Data, null);
        }

        public static async Task<(RaidResult result, string error)> ReportRaid(string raidId, bool won, bool partial, int seconds)
        {
            var body = new RaidResultRequest { won = won, partial = partial, seconds = Mathf.Clamp(seconds, 1, 360) };
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var r = await Client.Post<RaidResult>("/api/raids/" + raidId + "/result", body, Token);
                if (r.Ok && r.Data != null) return (r.Data, null);
                if (r.ErrorCode == "invalid_seconds" && body.seconds > 1) { body.seconds = Mathf.Max(1, body.seconds - 5); continue; }
                if (r.Outcome == ApiOutcome.Offline) { await Awaitable.WaitForSecondsAsync(2f); continue; }
                return (null, Error("raid result", r));
            }
            return (null, "The raid result could not be sent.");
        }

        /// <summary>Hackathon (12 Oct): a conquest raid's result (the server decides won / partial / lost).</summary>
        public static async Task<(RaidResult result, string error)> ReportConquest(string raidId, bool guardianSunk, int minuteReached, int seconds)
        {
            var body = new ConquestResultRequest { won = guardianSunk, guardianSunk = guardianSunk, minuteReached = Mathf.Clamp(minuteReached, 0, 16), seconds = Mathf.Clamp(seconds, 1, 1020) };
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var r = await Client.Post<RaidResult>("/api/raids/" + raidId + "/result", body, Token);
                if (r.Ok && r.Data != null) return (r.Data, null);
                if (r.Outcome != ApiOutcome.Offline) return (null, Error("raid result", r));
                await Awaitable.WaitForSecondsAsync(2f);
            }
            return (null, "The raid result could not be sent.");
        }

        // ---------------------------------------------------------------- defence designs

        public static async Task<(DefenceCatalog catalog, string error)> DefenceCatalog()
        {
            await OnlineService.EnsureConnected();
            var r = await Client.Get<DefenceCatalog>("/api/defence/catalog", Token);
            return r.Ok && r.Data != null && r.Data.kinds != null ? (r.Data, null) : (null, Error("defence catalog", r));
        }

        public static async Task<(IslandDefenceView view, string error)> Defence(string islandId)
        {
            var r = await Client.Get<IslandDefenceView>("/api/islands/" + islandId + "/defence", Token);
            return r.Ok && r.Data != null && r.Data.design != null ? (r.Data, null) : (null, Error("defence", r));
        }

        /// <summary>Saves a design (owner). On invalid_defence, <c>problems</c> lists every rejected field.</summary>
        public static async Task<(IslandDefenceView view, string error, DefenceProblem[] problems)> SaveDefence(string islandId, DefenceDesign design)
        {
            var r = await Client.Send<IslandDefenceView>("PUT", "/api/islands/" + islandId + "/defence", design, Token, 0);
            if (r.Ok && r.Data != null) return (r.Data, null, null);
            DefenceProblem[] problems = null;
            if (r.ErrorCode == "invalid_defence" && !string.IsNullOrEmpty(r.Body))
            {
                try { problems = JsonUtility.FromJson<DefenceErrorBody>(r.Body)?.errors; } catch (Exception) { }
            }
            return (null, r.ErrorCode == "invalid_defence" ? "The server rejected the design." : Error("save defence", r), problems);
        }

        public static async Task<(RaidResult result, string error)> SettleRaid(string raidId)
        {
            var r = await Client.Post<RaidResult>("/api/raids/" + raidId + "/settle", null, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("payout", r));
        }

        public static async Task<(MyRaids raids, string error)> MyRaids()
        {
            if (Token == null) return (null, "Sign in with a wallet first.");
            var r = await Client.Get<MyRaids>("/api/raids/mine", Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("raid history", r));
        }

        // ---------------------------------------------------------------- market

        public static async Task<(MarketView market, string error)> Market()
        {
            await OnlineService.EnsureConnected();
            var r = await Client.Get<MarketView>("/api/market", Token);
            if (r.Ok && r.Data != null)
            {
                if (!string.IsNullOrEmpty(r.Data.treasury)) Treasury = r.Data.treasury;
                return (r.Data, null);
            }
            return (null, Error("market", r));
        }

        public static async Task<(MarketView mine, string error)> MyListings()
        {
            if (Token == null) return (null, null);
            var r = await Client.Get<MarketView>("/api/market/mine", Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("your listings", r));
        }

        /// <summary>The end of a market payment or listing: the listing, the error, the transfer's signature (explorer link) and
        /// whether it is still pending (Finish purchase / Finish listing asks again without a second transfer).</summary>
        public sealed class TradeResult
        {
            public Listing Listing;
            public string Error;
            public string Signature;
            public bool Pending;
            /// <summary>The server refused it for good (the line says what happens to the SOL or the NFT).</summary>
            public bool Final;
            public bool Ok => Error == null && Listing != null;
        }

        /// <summary>Seams for the market posts (tests): listing id and signature; island id, price and signature.</summary>
        public static Func<string, string, Task<ApiResult<Listing>>> PostBuy = DefaultPostBuy;
        public static Func<string, long, string, Task<ApiResult<Listing>>> PostList = DefaultPostList;

        static async Task<ApiResult<Listing>> DefaultPostBuy(string listingId, string signature) =>
            await Client.Post<Listing>("/api/market/" + listingId + "/buy", new SignatureRequest { signature = signature }, Token);

        static async Task<ApiResult<Listing>> DefaultPostList(string islandId, long priceLamports, string signature) =>
            await Client.Post<Listing>("/api/market/list", new ListRequest { islandId = islandId, priceLamports = priceLamports, signature = signature }, Token);

        /// <summary>Seams for the reconciliation reads (tests): one listing; the player's listings.</summary>
        public static Func<string, Task<Listing>> FetchListing = DefaultFetchListing;
        public static Func<Task<Listing[]>> FetchMyListings = DefaultFetchMyListings;

        static async Task<Listing> DefaultFetchListing(string listingId)
        {
            var r = await Client.Get<Listing>("/api/market/" + listingId, Token);
            return r.Ok ? r.Data : null;
        }

        static async Task<Listing[]> DefaultFetchMyListings()
        {
            var (mine, _) = await MyListings();
            return mine?.listings;
        }

        public static void UseDefaultPosts()
        {
            PostBuy = DefaultPostBuy;
            PostList = DefaultPostList;
            FetchListing = DefaultFetchListing;
            FetchMyListings = DefaultFetchMyListings;
        }

        /// <summary>
        /// Pays the listing's price to the treasury (memo buy:&lt;id&gt;) and asks the server to swap. 13 Oct (review): the
        /// payment's signature is kept (<see cref="PendingTransfers"/>) when the wallet signs, before it is sent; a retry or
        /// a reopened market posts that signature again instead of paying twice. A listing no longer for sale after paying
        /// shows the server's answer (it refunds that payment).
        /// </summary>
        public static async Task<TradeResult> Buy(Listing l, Action<string> progress)
        {
            if (l == null) return new TradeResult { Error = "No listing." };
            if (!PendingTransfers.Has(PendingTransfers.Buy, l.listingId))
            {
                var session = Web3Session.Instance;
                if (!session.WalletConnected) return new TradeResult { Error = "Connect your wallet to buy." };
                var treasury = !string.IsNullOrEmpty(l.treasury) ? l.treasury : Treasury;
                if (string.IsNullOrEmpty(treasury)) return new TradeResult { Error = "The market has no escrow address." };
                // Do not pay for a listing that is gone.
                var fresh = await Client.Get<Listing>("/api/market/" + l.listingId, Token);
                if (fresh.Ok && fresh.Data != null && fresh.Data.status != "active") return new TradeResult { Error = "This listing is no longer for sale." };
                progress?.Invoke("Approve the " + HackathonRules.Sol(l.priceLamports) + " payment in your wallet...");
                var wallet = session.Address;
                var listingId = l.listingId;
                var t = await session.TransferSol(treasury, HackathonRules.LamportsToSol(l.priceLamports),
                    string.IsNullOrEmpty(l.memo) ? "buy:" + l.listingId : l.memo,
                    sig => PendingTransfers.Remember(PendingTransfers.Buy, listingId, sig, wallet, l.priceLamports));
                if (!t.Ok && !t.MaybeSent)
                {
                    PendingTransfers.Forget(PendingTransfers.Buy, l.listingId);
                    return new TradeResult { Error = t.Error ?? "The payment was not made." };
                }
            }
            return await FinishBuy(l.listingId, progress);
        }

        /// <summary>Posts the pending payment for <paramref name="listingId"/> (no wallet, no second payment).</summary>
        public static async Task<TradeResult> FinishBuy(string listingId, Action<string> progress, int attempts = 12)
        {
            var pending = PendingTransfers.Get(PendingTransfers.Buy, listingId);
            if (pending == null) return new TradeResult { Error = "No payment is waiting for this listing." };
            var price = HackathonRules.Sol(pending.lamports);
            var a = await PaymentFlow.Post(PendingTransfers.Buy, listingId, sig => PostBuy(listingId, sig),
                attempt => progress?.Invoke(attempt == 0 ? "Paid. The server is moving the island to you..." : "Waiting for devnet to confirm the payment..."),
                false, attempts);
            var r = new TradeResult { Signature = a.Signature };
            switch (a.Verdict)
            {
                case HackathonRules.PaymentVerdict.Accepted:
                    r.Listing = a.Data;
                    return r;
                case HackathonRules.PaymentVerdict.Final:
                    // An earlier post of this payment may have gone through (its reply lost): the listing says so.
                    if (a.Code == "listing_not_active" || a.Code == "signature_used")
                    {
                        Listing now = null;
                        try { now = await FetchListing(listingId); } catch (Exception e) { Debug.LogWarning("IslandApi.FinishBuy: " + e.Message); }
                        if (now != null && !string.IsNullOrEmpty(now.paymentSignature) && now.paymentSignature == a.Signature)
                        {
                            r.Listing = now;
                            return r;
                        }
                    }
                    r.Final = true;
                    r.Error = HackathonRules.FinalLine("payment", price, a.Code, a.Message);
                    return r;
                case HackathonRules.PaymentVerdict.Hold:
                    r.Pending = true;
                    r.Error = (string.IsNullOrEmpty(a.Message) ? "The server did not take the payment yet" : a.Message.TrimEnd('.')) +
                              $". Your {price} payment is kept: press Finish purchase to ask again (you will not pay twice).";
                    return r;
                default:
                    r.Pending = true;
                    r.Error = $"Devnet has not confirmed the {price} payment yet. Press Finish purchase in a moment (you will not pay twice).";
                    return r;
            }
        }

        public static async Task<(Listing listing, string error)> Complete(string listingId)
        {
            var r = await Client.Post<Listing>("/api/market/" + listingId + "/complete", null, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("trade", r));
        }

        public static async Task<(Listing listing, string error)> Cancel(string listingId)
        {
            var r = await Client.Post<Listing>("/api/market/" + listingId + "/cancel", null, Token);
            return r.Ok && r.Data != null ? (r.Data, null) : (null, Error("cancel", r));
        }

        /// <summary>
        /// Sends the island's NFT to the escrow, then lists it at <paramref name="priceLamports"/>. 13 Oct (review): the escrow
        /// transfer's signature is kept (<see cref="PendingTransfers"/>) when the wallet signs; "Finish listing" posts
        /// /api/market/list again with it, so an NFT that reached the escrow is never stuck there unlisted.
        /// </summary>
        public static async Task<TradeResult> List(IslandView island, long priceLamports, Action<string> progress)
        {
            if (island == null) return new TradeResult { Error = "No island." };
            if (priceLamports < 1_000_000L || priceLamports > 100_000_000_000L) return new TradeResult { Error = "Prices are 0.001 to 100 SOL." };
            if (!PendingTransfers.Has(PendingTransfers.List, island.islandId))
            {
                if (island.nft == null || !island.nft.Minted) return new TradeResult { Error = "The island's NFT is not minted yet (Retry mint first)." };
                var session = Web3Session.Instance;
                if (!session.WalletConnected) return new TradeResult { Error = "Connect your wallet to list the island." };
                var treasury = Treasury;
                if (string.IsNullOrEmpty(treasury))
                {
                    var (m, merr) = await Market();
                    if (merr != null) return new TradeResult { Error = merr };
                    treasury = m.treasury;
                }
                progress?.Invoke("Approve sending the Island NFT to the market escrow...");
                var wallet = session.Address;
                var islandId = island.islandId;
                var t = await session.TransferNft(island.nft.mint, treasury,
                    sig => PendingTransfers.Remember(PendingTransfers.List, islandId, sig, wallet, priceLamports));
                if (!t.Ok && !t.MaybeSent)
                {
                    PendingTransfers.Forget(PendingTransfers.List, island.islandId);
                    return new TradeResult { Error = t.Error ?? "The NFT was not sent." };
                }
            }
            return await FinishList(island.islandId, priceLamports, progress);
        }

        /// <summary>Lists an island whose NFT transfer to the escrow is pending (no second transfer). A price of 0 keeps the
        /// price asked when the NFT was sent.</summary>
        public static async Task<TradeResult> FinishList(string islandId, long priceLamports, Action<string> progress, int attempts = 12)
        {
            var pending = PendingTransfers.Get(PendingTransfers.List, islandId);
            if (pending == null) return new TradeResult { Error = "No NFT transfer is waiting for this island." };
            var price = priceLamports > 0 ? priceLamports : pending.lamports;
            var a = await PaymentFlow.Post(PendingTransfers.List, islandId, sig => PostList(islandId, price, sig),
                attempt => progress?.Invoke(attempt == 0 ? "NFT sent. The server is checking the escrow..." : "Waiting for devnet to confirm the NFT transfer..."),
                true, attempts);
            var r = new TradeResult { Signature = a.Signature };
            switch (a.Verdict)
            {
                case HackathonRules.PaymentVerdict.Accepted:
                    r.Listing = a.Data;
                    return r;
                case HackathonRules.PaymentVerdict.Final:
                    // An earlier post may have listed it (its reply lost): the player's listing with this escrow transfer.
                    if (a.Code == "already_listed" || a.Code == "signature_used")
                    {
                        Listing[] mine = null;
                        try { mine = await FetchMyListings(); } catch (Exception e) { Debug.LogWarning("IslandApi.FinishList: " + e.Message); }
                        if (mine != null)
                            foreach (var l in mine)
                                if (l != null && l.escrowSignature == a.Signature) { r.Listing = l; return r; }
                    }
                    r.Final = true;
                    r.Error = HackathonRules.FinalLine("NFT transfer", null, a.Code, a.Message);
                    return r;
                case HackathonRules.PaymentVerdict.Hold:
                    r.Pending = true;
                    r.Error = (string.IsNullOrEmpty(a.Message) ? "The server did not list the island yet" : a.Message.TrimEnd('.')) +
                              ". The NFT transfer is kept: press Finish listing to ask again (no second transfer).";
                    return r;
                default:
                    r.Pending = true;
                    r.Error = "The escrow has not seen the NFT yet. Press Finish listing in a moment (no second transfer).";
                    return r;
            }
        }

        // ---------------------------------------------------------------- errors

        public static string Error<T>(string what, ApiResult<T> r) where T : class
        {
            switch (r.ErrorCode)
            {
                case "already_has_island": return "You already have an island (buy more on the Market).";
                case "world_full": return "Every island slot is taken.";
                case "not_your_island": return "That island is not yours.";
                case "island_listed": return "The island is listed on the market (cancel the listing first).";
                case "invalid_kind": return "Unknown building.";
                case "max_level": return "That building is at its top level.";
                case "insufficient_rum": return "Not enough RUM. Collect your island or sail a voyage (1 RUM per 10 kills).";
                case "own_island": return "You cannot raid your own island.";
                case "island_shielded": return "That island is shielded after a recent raid.";
                case "raid_in_progress": return "Another captain is raiding that island right now.";
                case "raid_expired": return "The raid timed out (10 minutes).";
                case "raid_finished": return "That raid is already over.";
                case "mint_failed": return "The NFT mint failed on devnet. Try again in a moment.";
                case "nft_not_minted": return "The island's NFT is not minted yet.";
                case "already_listed": return "The island is already listed.";
                case "nft_not_received": return "The escrow did not receive the NFT.";
                case "signature_used": return "That transaction was already used.";
                case "invalid_price": return "Prices are 0.001 to 100 SOL.";
                case "listing_not_active": return "This listing is no longer for sale.";
                case "own_listing": return "That is your own listing.";
            }
            return HackathonApi.Error(what, r);
        }
    }
}
