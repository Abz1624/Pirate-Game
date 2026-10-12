using System;
using System.Threading.Tasks;
using RoguePirates.V4.Online;
using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): the island, raid and marketplace API calls (server/README.md, "Hackathon: islands, RUM,
    /// raids, island NFT market") with the wallet profile's session, and the wallet steps around them (stake deposit,
    /// purchase payment, NFT to escrow). Every call returns a player-facing error (or null) instead of throwing.
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

        /// <summary>Starts a raid (pays the 25 RUM fee); a stake of 0 raids without a wager.</summary>
        public static async Task<(RaidStart raid, string error)> StartRaid(string islandId, long stakeLamports)
        {
            if (Token == null) return (null, "Sign in with a wallet first (Wallet button, top of the menu).");
            var stake = stakeLamports > 0 ? stakeLamports.ToString() : "null";
            var body = new RawJsonBody("{\"islandId\":\"" + islandId + "\",\"stakeLamports\":" + stake + "}");
            var r = await Client.Post<RaidStart>("/api/raids/start", body, Token);
            return r.Ok && r.Data != null && !string.IsNullOrEmpty(r.Data.raidId) ? (r.Data, null) : (null, Error("raid", r));
        }

        /// <summary>Sends the raid's stake from the wallet to the treasury (memo raid:&lt;id&gt;) and has the server verify it.</summary>
        public static async Task<(RaidWager wager, string error)> FundRaid(RaidStart raid, Action<string> progress)
        {
            var w = raid?.wager;
            if (w == null || !w.Exists) return (null, "This raid has no stake.");
            var session = Web3Session.Instance;
            if (!session.WalletConnected) return (null, "Connect your wallet to send the stake.");
            progress?.Invoke("Approve the " + HackathonRules.Sol(w.stakeLamports) + " stake in your wallet...");
            var (sig, error) = await session.TransferSol(w.treasury, HackathonRules.LamportsToSol(w.stakeLamports),
                string.IsNullOrEmpty(w.memo) ? "raid:" + raid.raidId : w.memo);
            if (error != null) return (null, error);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                progress?.Invoke(attempt == 0 ? "Stake sent. The server is checking it on devnet..." : "Waiting for devnet to confirm the stake...");
                var r = await Client.Post<RaidWagerResponse>("/api/raids/" + raid.raidId + "/wager", new RaidWagerRequest { signature = sig }, Token);
                if (r.Ok && r.Data != null) return (r.Data.wager, null);
                if (r.ErrorCode == "tx_not_confirmed" || r.Outcome == ApiOutcome.Offline) { await Awaitable.WaitForSecondsAsync(2.5f); continue; }
                if (r.ErrorCode == "already_deposited") return (w, null);
                return (null, Error("stake", r));
            }
            return (null, "Devnet has not confirmed the stake yet (" + HackathonRules.ShortSig(sig) + ").");
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

        /// <summary>Pays the listing's price to the treasury (memo buy:&lt;id&gt;) and asks the server to swap.</summary>
        public static async Task<(Listing listing, string error)> Buy(Listing l, Action<string> progress)
        {
            var session = Web3Session.Instance;
            if (!session.WalletConnected) return (null, "Connect your wallet to buy.");
            var treasury = !string.IsNullOrEmpty(l.treasury) ? l.treasury : Treasury;
            if (string.IsNullOrEmpty(treasury)) return (null, "The market has no escrow address.");
            // Do not pay for a listing that is gone.
            var fresh = await Client.Get<Listing>("/api/market/" + l.listingId, Token);
            if (fresh.Ok && fresh.Data != null && fresh.Data.status != "active") return (null, "This listing is no longer for sale.");
            progress?.Invoke("Approve the " + HackathonRules.Sol(l.priceLamports) + " payment in your wallet...");
            var (sig, error) = await session.TransferSol(treasury, HackathonRules.LamportsToSol(l.priceLamports),
                string.IsNullOrEmpty(l.memo) ? "buy:" + l.listingId : l.memo);
            if (error != null) return (null, error);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                progress?.Invoke(attempt == 0 ? "Paid. The server is moving the island to you..." : "Waiting for devnet to confirm the payment...");
                var r = await Client.Post<Listing>("/api/market/" + l.listingId + "/buy", new SignatureRequest { signature = sig }, Token);
                if (r.Ok && r.Data != null) return (r.Data, null);
                if (r.ErrorCode == "tx_not_confirmed" || r.Outcome == ApiOutcome.Offline) { await Awaitable.WaitForSecondsAsync(2.5f); continue; }
                return (null, Error("purchase", r) + " (payment " + HackathonRules.ShortSig(sig) + ")");
            }
            return (null, "Devnet has not confirmed the payment yet (" + HackathonRules.ShortSig(sig) + "). Open Market again to finish.");
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

        /// <summary>Sends the island's NFT to the escrow, then lists it at <paramref name="priceLamports"/>.</summary>
        public static async Task<(Listing listing, string error)> List(IslandView island, long priceLamports, Action<string> progress)
        {
            if (island == null) return (null, "No island.");
            if (island.nft == null || !island.nft.Minted) return (null, "The island's NFT is not minted yet (Retry mint first).");
            if (priceLamports < 1_000_000L || priceLamports > 100_000_000_000L) return (null, "Prices are 0.001 to 100 SOL.");
            var session = Web3Session.Instance;
            if (!session.WalletConnected) return (null, "Connect your wallet to list the island.");
            var treasury = Treasury;
            if (string.IsNullOrEmpty(treasury))
            {
                var (m, merr) = await Market();
                if (merr != null) return (null, merr);
                treasury = m.treasury;
            }
            progress?.Invoke("Approve sending the Island NFT to the market escrow...");
            var (sig, error) = await session.TransferNft(island.nft.mint, treasury);
            if (error != null) return (null, error);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                progress?.Invoke(attempt == 0 ? "NFT sent. The server is checking the escrow..." : "Waiting for devnet to confirm the NFT transfer...");
                var r = await Client.Post<Listing>("/api/market/list", new ListRequest { islandId = island.islandId, priceLamports = priceLamports, signature = sig }, Token);
                if (r.Ok && r.Data != null) return (r.Data, null);
                if (r.ErrorCode == "tx_not_confirmed" || r.ErrorCode == "nft_not_received" || r.Outcome == ApiOutcome.Offline)
                {
                    await Awaitable.WaitForSecondsAsync(2.5f);
                    continue;
                }
                return (null, Error("listing", r) + " (NFT transfer " + HackathonRules.ShortSig(sig) + ")");
            }
            return (null, "The escrow has not seen the NFT yet (" + HackathonRules.ShortSig(sig) + "). Try listing again in a moment.");
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
