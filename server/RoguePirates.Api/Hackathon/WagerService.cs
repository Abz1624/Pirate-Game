using Microsoft.EntityFrameworkCore;
using RoguePirates.Api.Data;
using RoguePirates.Api.Playtest;
using RoguePirates.Api.Solana;

namespace RoguePirates.Api.Hackathon;

public sealed record WagerStakeRequest(long StakeLamports);
public sealed record WagerDepositRequest(string? Signature);

/// <summary>
/// Hackathon: 1v1 SOL wagers on the lobby/match flow (devnet). The host puts a stake on a lobby; before the match can
/// start each human seat deposits the stake to the treasury (a transfer with memo <c>wager:&lt;lobbyId&gt;</c>, verified
/// by <see cref="LedgerService.VerifyDepositAsync"/>). Against the practice bot / AI captain the house matches the stake.
/// When the match is decided (relay referee or the two reports) the winner is paid 2x the stake; a draw, a dispute or an
/// abandoned match refunds every deposit; a lobby closed or expired before the start refunds too, and a guest who leaves
/// before the start gets their deposit back. Payouts go through <see cref="PayoutOnce"/> (once each, retried on failure).
///
/// State changes happen under <see cref="GameLocks.Lobby"/>; chain calls (verify, pay) happen outside it.
/// </summary>
public sealed class WagerService
{
    public const long MinStakeLamports = 1_000_000; // 0.001 SOL
    public const long DefaultMaxStakeLamports = SolanaService.LamportsPerSol / 2; // 0.5 SOL
    public const string DepositPurpose = "wager";
    public const string WinReason = "wager_win", RefundReason = "wager_refund";
    /// <summary>A deposit verified but not yet attached to its seat is left alone this long (the attach is in flight).</summary>
    public static readonly TimeSpan AttachGrace = TimeSpan.FromMinutes(2);
    /// <summary>A started match that was never decided (its room was lost) is refunded after this.</summary>
    public static readonly TimeSpan AbandonAfter = ArenaRules.Countdown + ArenaRules.MatchLength + TimeSpan.FromMinutes(5);

    readonly GameDb _db;
    readonly SolanaService _sol;
    readonly LedgerService _ledger;
    readonly LobbyService _lobbies;
    readonly IClock _clock;
    readonly IConfiguration _config;
    readonly ApiSettings _settings;
    readonly ILogger<WagerService> _log;

    public WagerService(GameDb db, SolanaService sol, LedgerService ledger, LobbyService lobbies, IClock clock,
        IConfiguration config, ApiSettings settings, ILogger<WagerService> log)
    {
        _settings = settings;
        _db = db;
        _sol = sol;
        _ledger = ledger;
        _lobbies = lobbies;
        _clock = clock;
        _config = config;
        _log = log;
    }

    /// <summary>The stake cap (WAGER_MAX_LAMPORTS, default 0.5 SOL).</summary>
    public long MaxStakeLamports => long.TryParse(_config["WAGER_MAX_LAMPORTS"], out var v) && v >= MinStakeLamports ? v : DefaultMaxStakeLamports;

    public static string Memo(Guid lobbyId) => "wager:" + lobbyId;
    static string DepositRef(long depositId) => "dep:" + depositId;

    static bool PreStart(Lobby l) => l.Status is LobbyStates.Waiting or LobbyStates.Full;
    static bool Open(Wager w) => w.Status is Wager.AwaitingDeposits or Wager.Funded;

    // ------------------------------------------------------------ static helpers used by LobbyService

    /// <summary>The lobby's current wager (the newest one not cancelled), or null.</summary>
    public static Task<Wager?> ForLobbyAsync(GameDb db, Guid lobbyId) =>
        db.Wagers.Where(w => w.LobbyId == lobbyId && w.Status != Wager.Cancelled).OrderByDescending(w => w.CreatedAt).FirstOrDefaultAsync();

    /// <summary>Both stakes are in: the host's deposit and the guest's. Never against the practice bot (13 Oct hotfix: a bot
    /// match is decided by the host's client, so it is free play; the house no longer matches stakes).</summary>
    public static bool IsFunded(Wager w, Lobby lobby) =>
        w.HostDepositSig != null && lobby.GuestPlayerId != null && !PracticeBot.Is(lobby.GuestPlayerId)
        && w.GuestDepositSig != null && w.GuestPlayerId == lobby.GuestPlayerId;

    /// <summary>V5 M9h switch WAGERS_HUMAN_ENABLED=false: no new SOL stakes or deposits (open wagers still settle).</summary>
    public static ApiException WagersOff() => new(409, "wagers_off",
        "SOL stakes are switched off on this server; play the match without a stake.");

    public static ApiException BotStakesOff() => new(409, "bot_stakes_off",
        "Matches against Captain Marrow (the practice bot) are free play: remove the stake (stakeLamports 0) or wait for a human opponent.");

    /// <summary>Refuses seating the practice bot in a lobby with an open stake (13 Oct hotfix: no staked bot matches).</summary>
    public static async Task RefuseBotIfStakedAsync(GameDb db, Lobby lobby)
    {
        var w = await ForLobbyAsync(db, lobby.Id);
        if (w != null && Open(w)) throw BotStakesOff();
    }

    /// <summary>The start gate: true when the lobby has no open wager, or its wager is funded.</summary>
    public static async Task<bool> MayStartAsync(GameDb db, Lobby lobby)
    {
        var w = await ForLobbyAsync(db, lobby.Id);
        return w == null || !Open(w) || IsFunded(w, lobby);
    }

    /// <summary>A new wager row for a lobby being created (validated by <see cref="ValidateStake"/> first).</summary>
    public static Wager NewForLobby(Guid lobbyId, Guid hostId, long stake, DateTime now) => new()
    {
        Id = Guid.NewGuid(), Kind = "pvp", LobbyId = lobbyId, StakeLamports = stake, HostPlayerId = hostId,
        Status = Wager.AwaitingDeposits, CreatedAt = now,
    };

    /// <summary>The wager as shown in the lobby JSON (null when the lobby has none).</summary>
    public static async Task<object?> LobbyViewAsync(GameDb db, Lobby lobby, string? treasury, string cluster)
    {
        var w = await ForLobbyAsync(db, lobby.Id);
        return w == null ? null : await ViewAsync(db, w, lobby, treasury, cluster);
    }

    public static async Task<object> ViewAsync(GameDb db, Wager w, Lobby? lobby, string? treasury, string cluster)
    {
        var guestId = lobby != null && Open(w) ? lobby.GuestPlayerId : w.GuestPlayerId ?? (w.VsHouse ? PracticeBot.Id : null);
        var vsHouse = Open(w) ? PracticeBot.Is(guestId) : w.VsHouse;
        var funded = lobby != null && Open(w) ? IsFunded(w, lobby) : w.Status is Wager.Funded or Wager.Settled;
        var refId = w.Id.ToString();
        var depositRefs = await db.SolanaDeposits.AsNoTracking()
            .Where(d => d.Purpose == DepositPurpose && d.RefId == refId).Select(d => d.Id).ToListAsync();
        var refs = depositRefs.Select(DepositRef).Append(refId).ToList();
        var payouts = await db.Payouts.AsNoTracking()
            .Where(p => (p.Reason == WinReason || p.Reason == RefundReason) && p.RefId != null && refs.Contains(p.RefId))
            .OrderBy(p => p.Id).ToListAsync();
        var guestDeposited = w.GuestDepositSig != null && (!Open(w) || w.GuestPlayerId == guestId);
        string? result = w.Status switch
        {
            Wager.Settled when w.WinnerPlayerId is { } win && PracticeBot.Is(win) => "house_won",
            Wager.Settled => "paid_winner",
            Wager.Refunded => "refunded",
            Wager.Cancelled => "cancelled",
            _ => null,
        };
        return new
        {
            wagerId = w.Id, kind = w.Kind, lobbyId = w.LobbyId, matchId = w.MatchId,
            stakeLamports = w.StakeLamports, stakeSol = w.StakeLamports / (double)SolanaService.LamportsPerSol,
            potLamports = w.StakeLamports * 2, vsHouse, status = w.Status, funded, result,
            memo = w.LobbyId is { } lid ? Memo(lid) : null, treasury, cluster,
            host = new { playerId = w.HostPlayerId, deposited = w.HostDepositSig != null, signature = w.HostDepositSig },
            guest = new
            {
                playerId = guestId, house = vsHouse,
                deposited = vsHouse || guestDeposited, signature = vsHouse ? null : guestDeposited ? w.GuestDepositSig : null,
            },
            winnerPlayerId = w.WinnerPlayerId,
            payoutSignature = w.PayoutSig,
            payouts = payouts.Select(p => LedgerService.PayoutView(p, cluster)),
            createdAt = w.CreatedAt, settledAt = w.SettledAt,
        };
    }

    // ------------------------------------------------------------ stake

    /// <summary>Checks a stake before it is put on a lobby: the host signed in with a wallet, the treasury is on, 0.001..cap SOL.</summary>
    public void ValidateStake(Player host, long stake)
    {
        if (!_settings.WagersHumanEnabled) throw WagersOff();
        if (host.WalletAddress == null)
            throw new ApiException(400, "wallet_required", "Sign in with a wallet to put a stake on a lobby.");
        if (!_sol.TreasuryOn) throw new ApiException(503, "treasury_off", "The treasury wallet is not configured on this server.");
        if (stake < MinStakeLamports || stake > MaxStakeLamports)
            throw new ApiException(400, "invalid_stake", $"stakeLamports must be {MinStakeLamports}..{MaxStakeLamports} (0.001..{MaxStakeLamports / (double)SolanaService.LamportsPerSol} SOL).");
    }

    /// <summary>Host only, before the start and before any deposit: sets (or with 0 removes) the lobby's stake.</summary>
    public async Task<object> SetStakeAsync(Player player, Guid lobbyId, long stake)
    {
        if (stake != 0) ValidateStake(player, stake);
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await LoadLobbyAsync(lobbyId);
            if (lobby.HostPlayerId != player.Id) throw new ApiException(403, "not_host", "Only the host can set the stake.");
            if (!PreStart(lobby)) throw new ApiException(409, "already_started", "The stake can only change before the match starts.");
            if (stake != 0 && PracticeBot.Is(lobby.GuestPlayerId)) throw BotStakesOff();
            var w = await ForLobbyAsync(_db, lobbyId);
            if (w != null && (w.HostDepositSig != null || w.GuestDepositSig != null || !Open(w)))
                throw new ApiException(409, "wager_locked", "A deposit was already made; the stake cannot change.");
            if (stake == 0)
            {
                if (w != null) w.Status = Wager.Cancelled;
            }
            else if (w != null) w.StakeLamports = stake;
            else _db.Wagers.Add(NewForLobby(lobbyId, lobby.HostPlayerId, stake, _clock.UtcNow));
            await _db.SaveChangesAsync();
            return await _lobbies.OnWagerChangedAsync(lobby);
        }
        finally { GameLocks.Lobby.Release(); }
    }

    // ------------------------------------------------------------ deposit

    /// <summary>
    /// A seated player's stake deposit: verifies the transfer (memo wager:&lt;lobbyId&gt;, at least the stake), attaches it
    /// to their seat and, when both stakes are in and both seats are ready, starts the match. Returns the lobby state.
    /// Since 12 Oct (the client signs the deposit as part of creating or joining): a valid transfer is always verified and
    /// recorded first; when the seat or wager is no longer valid (left or not seated, wager closed, match started, stake
    /// already paid) it is refunded once and the call fails with 409 deposit_refunded, so no SOL is ever stranded.
    /// V5 M9h: the same for a transfer that is too small or carries another memo (before: 400 amount_too_low /
    /// memo_mismatch and nothing recorded): recorded, refunded once (wager_refund, refId dep:&lt;depositId&gt;) and answered
    /// with 409 deposit_refunded naming the reason.
    /// </summary>
    public async Task<object> DepositAsync(Player player, Guid lobbyId, string? signature)
    {
        Wager w;
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await LoadLobbyAsync(lobbyId);
            // any wager of this lobby, a cancelled one too (closed while the player signed: the transfer is refunded)
            w = await ForLobbyAsync(_db, lobbyId)
                ?? await _db.Wagers.Where(x => x.LobbyId == lobbyId).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync()
                ?? throw new ApiException(404, "no_wager", "This lobby has no stake.");
            var mine = lobby.HostPlayerId == player.Id ? w.HostDepositSig : w.GuestPlayerId == player.Id ? w.GuestDepositSig : null;
            if (mine != null && mine == (signature ?? "").Trim()) return await _lobbies.BuildStateAsync(lobby); // idempotent
            // Seat and wager checks come after the chain check: a transfer already sent is recorded and refunded below.
        }
        finally { GameLocks.Lobby.Release(); }

        // chain check outside the lobby lock. V5 M9h: any amount and any memo, so a transfer from the player's wallet to the
        // treasury that cannot pay the stake (too little, another memo or none) is recorded too and refunded below, once
        // (it used to fail with amount_too_low / memo_mismatch and leave the SOL in the treasury).
        // (review) only a transfer made after the wager existed (2 minutes' clock tolerance): an older transfer to the
        // treasury (a top-up, a payment for something else) is refused unrecorded, never refunded through a wager
        var (deposit, _) = await _ledger.VerifyDepositAsync(player, signature, DepositPurpose, w.Id.ToString(), 1, null,
            notBefore: w.CreatedAt - TimeSpan.FromMinutes(2));

        string? refuse = null;
        object? state = null;
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await LoadLobbyAsync(lobbyId);
            await _db.Entry(lobby).ReloadAsync();
            await _db.Entry(w).ReloadAsync();
            var d = await _db.SolanaDeposits.FirstAsync(x => x.Id == deposit.Id);
            await _db.Entry(d).ReloadAsync();
            Reconcile(w, lobby);
            var isHost = lobby.HostPlayerId == player.Id;
            var attached = d.Signature == (isHost ? w.HostDepositSig : w.GuestDepositSig);
            // 13 Oct hotfix: a deposit with any refund decided (refunding/refunded, or a refund payout recorded) is never attached again
            var refundRef = DepositRef(d.Id);
            if (d.Status is SolanaDeposit.Refunding or SolanaDeposit.Refunded
                || await _db.Payouts.AnyAsync(p => p.Reason == RefundReason && p.RefId == refundRef))
                throw new ApiException(409, "deposit_refunded", "This deposit is refunded (or being refunded) to your wallet.");
            if (!attached)
            {
                try { CheckSeat(lobby, w, player); }
                catch (ApiException ex) { refuse = ex.Message; }
                // 13 Oct hotfix: the stored deposit is re-checked against the CURRENT wager under the lock: the stake may have
                // been raised after it was verified, and a stored deposit is returned without a new chain check
                refuse ??= DepositProblem(d, w, player, lobbyId);
                if (refuse == null && !_settings.WagersHumanEnabled) refuse = WagersOff().Message;
                if (refuse == null && (isHost ? w.HostDepositSig : w.GuestDepositSig) is { } other && other != d.Signature)
                    refuse = "Your stake was already deposited.";
                if (refuse == null && d.Status != SolanaDeposit.Verified)
                    refuse = "This deposit was already used and released; it is refunded.";
            }

            if (attached || d.Signature == w.HostDepositSig || d.Signature == w.GuestDepositSig) { /* already attached */ }
            else if (refuse == null)
            {
                if (isHost) w.HostDepositSig = d.Signature;
                else
                {
                    w.GuestPlayerId = player.Id;
                    w.GuestDepositSig = d.Signature;
                }
            }
            if (d.Status == SolanaDeposit.Verified)
            {
                // consumed: attached to the wager, or (refused) detached and refunded below
                d.Status = SolanaDeposit.Consumed;
                d.ConsumedAt = _clock.UtcNow;
            }
            Reconcile(w, lobby);
            await _db.SaveChangesAsync();
            if (refuse == null) state = await _lobbies.OnWagerChangedAsync(lobby);
        }
        finally { GameLocks.Lobby.Release(); }

        if (refuse != null)
        {
            await ProcessPaymentsAsync(w.Id);
            var refunded = await _db.SolanaDeposits.AsNoTracking().AnyAsync(x => x.Id == deposit.Id && x.Status == SolanaDeposit.Refunded);
            throw new ApiException(409, "deposit_refunded", refuse + (refunded
                ? " Your deposit was refunded to your wallet."
                : " Your deposit is being refunded to your wallet."));
        }
        _log.LogInformation("Wager {WagerId}: deposit {Sig} from player {PlayerId}", w.Id, deposit.Signature, player.Id);
        return state!;
    }

    /// <summary>Why a recorded deposit cannot pay this player's stake now (null when it can): right player and wallet,
    /// this wager's deposit, the lobby's memo, at least the current stake (the recipient was checked to be the treasury
    /// when the deposit was recorded; only the server records wager deposits).</summary>
    public static string? DepositProblem(SolanaDeposit d, Wager w, Player player, Guid lobbyId)
    {
        if (d.PlayerId != player.Id || d.Wallet != player.WalletAddress) return "This deposit is not from your wallet.";
        if (d.Purpose != DepositPurpose || d.RefId != w.Id.ToString()) return "This deposit is not for this wager.";
        if (d.Memo == null || !d.Memo.Contains(Memo(lobbyId), StringComparison.Ordinal)) return "The deposit's memo does not name this lobby.";
        if (d.Lamports < w.StakeLamports)
            return $"The deposit is {d.Lamports} lamports; the stake is {w.StakeLamports}.";
        return null;
    }

    void CheckSeat(Lobby lobby, Wager w, Player player)
    {
        if (lobby.HostPlayerId != player.Id && lobby.GuestPlayerId != player.Id)
            throw new ApiException(403, "not_in_lobby", "You are not seated in this lobby.");
        if (!Open(w)) throw new ApiException(409, "wager_closed", "This wager is " + w.Status + ".");
        if (!PreStart(lobby)) throw new ApiException(409, "already_started", "Stakes are deposited before the match starts.");
    }

    /// <summary>
    /// Brings an open wager in line with its lobby (no chain calls; the caller saves): a guest who left before the start
    /// is detached (their deposit gets refunded), the house flag follows the guest seat, the funded flag follows the
    /// deposits; a lobby closed or expired before the start refunds the wager (or cancels it without deposits).
    /// </summary>
    void Reconcile(Wager w, Lobby lobby)
    {
        if (!Open(w)) return;
        if (PreStart(lobby))
        {
            if (w.GuestPlayerId != null && w.GuestPlayerId != lobby.GuestPlayerId)
            {
                w.GuestPlayerId = null;
                w.GuestDepositSig = null;
            }
            w.VsHouse = PracticeBot.Is(lobby.GuestPlayerId);
            w.Status = IsFunded(w, lobby) ? Wager.Funded : Wager.AwaitingDeposits;
        }
        else if (lobby.MatchId == null)
        {
            // closed or expired before a match started
            w.Status = w.HostDepositSig == null && w.GuestDepositSig == null ? Wager.Cancelled : Wager.Refunded;
            w.SettledAt = _clock.UtcNow;
        }
        else
        {
            w.MatchId ??= lobby.MatchId;
            if (PracticeBot.Is(lobby.GuestPlayerId)) { w.VsHouse = true; w.GuestPlayerId = null; }
        }
    }

    // ------------------------------------------------------------ settlement

    /// <summary>
    /// Reconciles the lobby's open wager and, once its match is decided, settles it: the winner gets 2x the stake (vs the
    /// house: only a human winner is paid); draw, dispute or a match abandoned for <see cref="AbandonAfter"/> refunds.
    /// Then sends what is owed. Safe to call any number of times.
    /// </summary>
    public async Task SettleLobbyAsync(Guid lobbyId)
    {
        Guid? wagerId = null;
        var changed = false;
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId);
            var w = lobby == null ? null : await ForLobbyAsync(_db, lobbyId);
            if (lobby == null || w == null) return;
            wagerId = w.Id;
            var before = (w.Status, w.GuestDepositSig, w.VsHouse);
            Reconcile(w, lobby);
            if (w.Status == Wager.Funded && !PreStart(lobby) && w.MatchId is { } matchId)
                await DecideAsync(w, matchId);
            changed = before != (w.Status, w.GuestDepositSig, w.VsHouse);
            await _db.SaveChangesAsync();
            if (changed) await _lobbies.PublishAsync(lobby);
        }
        finally { GameLocks.Lobby.Release(); }

        if (wagerId is { } id && await ProcessPaymentsAsync(id) && changed) await PublishAsync(lobbyId);
    }

    async Task DecideAsync(Wager w, Guid matchId)
    {
        var match = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) return;
        var now = _clock.UtcNow;
        if (w.VsHouse && match.Status is MatchStates.Decided or MatchStates.Disputed)
        {
            // 13 Oct hotfix: a bot match is decided by the host's client, so a stake on it (from before the hotfix) is
            // refunded whatever the result; the house never pays out on it
            w.Status = Wager.Refunded;
            w.SettledAt = now;
            _log.LogInformation("Wager {WagerId} vs the house refunded (bot matches are free play)", w.Id);
        }
        else if (match.Status == MatchStates.Decided && match.WinnerPlayerId is { } winner)
        {
            w.WinnerPlayerId = winner;
            w.Status = Wager.Settled;
            w.SettledAt = now;
            _log.LogInformation("Wager {WagerId} settled: winner {Winner}{House}", w.Id, winner, PracticeBot.Is(winner) ? " (the house keeps the stake)" : "");
        }
        else if (match.Status is MatchStates.Decided or MatchStates.Disputed || match.CreatedAt + AbandonAfter <= now)
        {
            w.Status = Wager.Refunded;
            w.SettledAt = now;
            _log.LogInformation("Wager {WagerId} refunded: match {MatchId} {Status}", w.Id, matchId, match.Status);
        }
    }

    /// <summary>
    /// Sends what a wager owes, each obligation once: refunds of deposits no longer attached to a seat (or every deposit
    /// when the wager is refunded), and the winner's 2x stake. Returns true when a payout was recorded or changed.
    /// </summary>
    public async Task<bool> ProcessPaymentsAsync(Guid wagerId)
    {
        var w = await _db.Wagers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == wagerId);
        if (w == null || !_sol.TreasuryOn) return false;
        var refId = w.Id.ToString();
        var now = _clock.UtcNow;
        var any = false;

        var candidates = await _db.SolanaDeposits.AsNoTracking()
            .Where(d => d.Purpose == DepositPurpose && d.RefId == refId && d.Status != SolanaDeposit.Refunded).Select(d => d.Id).ToListAsync();
        foreach (var depositId in candidates)
        {
            // 13 Oct hotfix: the refund decision is taken under the lobby lock (where deposits are attached) and marked
            // "refunding" first, so a deposit being refunded can never be attached to a seat again
            SolanaDeposit? d = null;
            await GameLocks.Lobby.WaitAsync();
            try
            {
                var current = await _db.Wagers.AsNoTracking().FirstAsync(x => x.Id == wagerId);
                var dep = await _db.SolanaDeposits.FirstAsync(x => x.Id == depositId);
                await _db.Entry(dep).ReloadAsync();
                var attached = dep.Signature == current.HostDepositSig || dep.Signature == current.GuestDepositSig;
                var owed = dep.Status != SolanaDeposit.Refunded && (current.Status is Wager.Refunded or Wager.Cancelled || !attached);
                var inFlight = !attached && dep.Status == SolanaDeposit.Verified && dep.CreatedAt + AttachGrace > now; // attach in flight
                if (owed && !inFlight)
                {
                    if (dep.Status != SolanaDeposit.Refunding)
                    {
                        dep.Status = SolanaDeposit.Refunding;
                        dep.ConsumedAt ??= now;
                        await _db.SaveChangesAsync();
                    }
                    d = dep;
                }
            }
            finally { GameLocks.Lobby.Release(); }
            if (d == null) continue;
            var p = await PayoutOnce.PayAsync(_db, _ledger, _clock, _log, d.PlayerId, d.Wallet, d.Lamports, RefundReason, DepositRef(d.Id));
            if (p == null) continue;
            any = true;
            if (p.Status == Payout.Sent)
            {
                d.Status = SolanaDeposit.Refunded;
                await _db.SaveChangesAsync();
            }
        }

        if (w.Status == Wager.Settled && w.WinnerPlayerId is { } winnerId && !PracticeBot.Is(winnerId) && w.PayoutSig == null)
        {
            var winner = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == winnerId);
            if (winner?.WalletAddress is { } wallet)
            {
                var p = await PayoutOnce.PayAsync(_db, _ledger, _clock, _log, winnerId, wallet, w.StakeLamports * 2, WinReason, refId);
                if (p != null)
                {
                    any = true;
                    var tracked = await _db.Wagers.FirstAsync(x => x.Id == w.Id);
                    tracked.PayoutId = p.Id;
                    if (p.Status == Payout.Sent) tracked.PayoutSig = p.Signature;
                    await _db.SaveChangesAsync();
                }
            }
            else _log.LogWarning("Wager {WagerId}: winner {Winner} has no wallet; not paid", w.Id, winnerId);
        }
        return any;
    }

    async Task PublishAsync(Guid lobbyId)
    {
        await GameLocks.Lobby.WaitAsync();
        try
        {
            var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId);
            if (lobby != null)
            {
                await _db.Entry(lobby).ReloadAsync();
                await _lobbies.PublishAsync(lobby);
            }
        }
        finally { GameLocks.Lobby.Release(); }
    }

    /// <summary>Sweeper pass: every open wager is reconciled/settled; recent closed ones get their owed payouts retried.</summary>
    public async Task<int> SweepAsync()
    {
        var since = _clock.UtcNow.AddDays(-7);
        var rows = await _db.Wagers.AsNoTracking()
            .Where(w => w.LobbyId != null && (w.Status == Wager.AwaitingDeposits || w.Status == Wager.Funded || w.CreatedAt >= since))
            .Select(w => new { w.Id, LobbyId = w.LobbyId!.Value, w.Status })
            .ToListAsync();
        foreach (var r in rows)
        {
            try
            {
                if (r.Status is Wager.AwaitingDeposits or Wager.Funded) await SettleLobbyAsync(r.LobbyId);
                else await ProcessPaymentsAsync(r.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Wager sweep failed for lobby {LobbyId}", r.LobbyId);
            }
        }
        return rows.Count;
    }

    /// <summary>Runs <see cref="SettleLobbyAsync"/> in the background on its own scope (after a match end or a seat change).</summary>
    public static void Kick(IServiceScopeFactory scopes, Guid lobbyId, ILogger log)
    {
        var scope = scopes.CreateScope();
        var task = Task.Run(async () =>
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<WagerService>().SettleLobbyAsync(lobbyId);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Wager settlement for lobby {LobbyId} failed (the sweeper retries)", lobbyId);
            }
            finally { scope.Dispose(); }
        });
        scope.ServiceProvider.GetService<BackgroundSettlements>()?.Track(task);
    }

    // ------------------------------------------------------------ reads

    public async Task<object> GetAsync(Guid lobbyId)
    {
        var lobby = await _db.Lobbies.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lobbyId)
                    ?? throw new ApiException(404, "lobby_not_found", "No such lobby.");
        var w = await ForLobbyAsync(_db, lobbyId) ?? throw new ApiException(404, "no_wager", "This lobby has no stake.");
        return await ViewAsync(_db, w, lobby, _sol.TreasuryAddress, _sol.Cluster);
    }

    public async Task<object> MineAsync(Player player)
    {
        var list = await _db.Wagers.AsNoTracking()
            .Where(w => w.HostPlayerId == player.Id || w.GuestPlayerId == player.Id)
            .OrderByDescending(w => w.CreatedAt).Take(20).ToListAsync();
        var views = new List<object>();
        foreach (var w in list)
        {
            var lobby = w.LobbyId is { } lid ? await _db.Lobbies.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid) : null;
            views.Add(await ViewAsync(_db, w, lobby, _sol.TreasuryAddress, _sol.Cluster));
        }
        return new { wagers = views };
    }

    async Task<Lobby> LoadLobbyAsync(Guid lobbyId) =>
        await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId) ?? throw new ApiException(404, "lobby_not_found", "No such lobby.");
}

/// <summary>
/// The wager settlements started in the background by <see cref="WagerService.Kick"/> (after a seat leaves or a match
/// ends). Nothing in the API waits for them (the sweeper retries anything they miss); <see cref="WhenIdleAsync"/> lets a
/// caller (tests, shutdown) wait until every one started so far has finished.
/// </summary>
public sealed class BackgroundSettlements
{
    readonly object _gate = new();
    readonly List<Task> _running = new();

    public void Track(Task task)
    {
        lock (_gate)
        {
            _running.RemoveAll(t => t.IsCompleted);
            _running.Add(task);
        }
    }

    public async Task WhenIdleAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_gate)
            {
                _running.RemoveAll(t => t.IsCompleted);
                pending = _running.ToArray();
            }
            if (pending.Length == 0) return;
            await Task.WhenAll(pending);
        }
    }
}

/// <summary>Hackathon: settles decided wagers, refunds closed lobbies and retries failed wager payouts (every SWEEP_SECONDS).</summary>
public sealed class WagerSweeper : BackgroundService
{
    readonly IServiceScopeFactory _scopes;
    readonly ApiSettings _settings;
    readonly ILogger<WagerSweeper> _log;

    public WagerSweeper(IServiceScopeFactory scopes, ApiSettings settings, ILogger<WagerSweeper> log)
    {
        _scopes = scopes;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.SweepSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PassAsync(_scopes, _log);
        }
    }

    static async Task StepAsync(IServiceScopeFactory scopes, ILogger log, string name, Func<IServiceProvider, Task> step)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await step(scope.ServiceProvider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "{Step} failed", name);
        }
    }

    /// <summary>One sweep: payouts reconciled by signature first (13 Oct hotfix), then wagers, then market refunds.</summary>
    public static async Task PassAsync(IServiceScopeFactory scopes, ILogger log)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LedgerService>().ReconcileAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Payout reconcile failed");
        }
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WagerService>().SweepAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Wager sweep failed");
        }
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MarketService>().SweepRefundsAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Market refund sweep failed");
        }
        // V5 M9h: pending Island NFT mints resolved on chain (never re-sent), the treasury's low-float alarm, and raids past
        // their timeout stored as abandoned (reads no longer write them); each step on its own so one failure skips no other
        await StepAsync(scopes, log, "Mint reconcile", sp => sp.GetRequiredService<IslandService>().ReconcileMintsAsync());
        await StepAsync(scopes, log, "Treasury check", sp => sp.GetRequiredService<TreasuryMonitor>().BalanceAsync());
        await StepAsync(scopes, log, "Raid expiry", async sp =>
        {
            await IslandLocks.Economy.WaitAsync();
            try { await sp.GetRequiredService<RaidService>().ExpireStaleAsync(); }
            finally { IslandLocks.Economy.Release(); }
        });
        try
        {
            // V5 M9h: expired sessions are deleted
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            await WalletAuthService.PurgeExpiredAsync(sp.GetRequiredService<GameDb>(), sp.GetRequiredService<IClock>().UtcNow,
                sp.GetRequiredService<PlayerAuth>().SessionLifetime);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Session purge failed");
        }
    }
}
