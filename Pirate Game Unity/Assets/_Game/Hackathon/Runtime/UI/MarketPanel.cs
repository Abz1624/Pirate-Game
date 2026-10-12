using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct, U-3): the menu's "Market" screen, the Island NFT marketplace with the server's escrow wallet
    /// (the treasury): the active listings (GET /api/market) with Buy (devnet SOL to the escrow with the memo
    /// buy:&lt;id&gt;, then POST /api/market/{id}/buy: the server moves the NFT and the island to the buyer and pays the
    /// seller); "Sell your island" (the Island NFT goes to the escrow by an SPL transfer, then POST /api/market/list);
    /// your listings with Cancel (the NFT comes back) and Complete for a paid trade that stopped half way.
    /// </summary>
    public sealed class MarketPanel : MonoBehaviour
    {
        RectTransform _list;
        TextMeshProUGUI _status, _sub;
        Button _close;
        TMP_InputField _price;
        MarketView _market, _mine;
        MyIslands _islands;
        bool _busy;
        int _requests;

        public bool IsOpen => gameObject.activeSelf;
        public MarketView Listings => _market;
        public MarketView MyListings => _mine;
        public string StatusText => _status != null ? _status.text : null;
        public bool Busy => _busy;
        /// <summary>The Islands screen (refreshed after a trade).</summary>
        public IslandsPanel Islands { get; set; }

        public static MarketPanel Create(Transform canvas)
        {
            var rt = HackUi.Panel(canvas, "HackathonMarket", new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f), 228);
            var panel = rt.gameObject.AddComponent<MarketPanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var header = HackUi.TopStrip(rt, "Header", 14f, 80f);
            HackUi.TextAt(header, "Title", "Island market", 46, TextAlignmentOptions.MidlineLeft, HackUi.Gold, Vector2.zero, new Vector2(0.45f, 1f), true);
            HackUi.Button(header, "Refresh", Refresh, new Vector2(0.6f, 0.06f), new Vector2(0.79f, 0.94f));
            _close = HackUi.Button(header, "Close", Close, new Vector2(0.81f, 0.06f), new Vector2(1f, 0.94f));
            var sub = HackUi.TopStrip(rt, "Sub", 98f, 76f);
            _sub = HackUi.TextAt(sub, "Text", "Island NFTs for devnet SOL. The game's escrow holds a listed NFT and swaps it for the payment.", 24,
                TextAlignmentOptions.TopLeft, HackUi.Dim, Vector2.zero, new Vector2(1f, 1f));
            _sub.textWrappingMode = TextWrappingModes.Normal;
            _status = HackUi.TextAt(sub, "Status", string.Empty, 25, TextAlignmentOptions.BottomLeft, HackUi.Dim, Vector2.zero, Vector2.one);
            _status.textWrappingMode = TextWrappingModes.Normal;
            var body = HackUi.Area(rt, "Body", Vector2.zero, Vector2.one, new Vector4(24f, 20f, 24f, 184f));
            _list = HackUi.ScrollList(body, "Scroll", Vector4.zero);
            _list.GetComponent<VerticalLayoutGroup>().spacing = 8f;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (Islands != null && Islands.IsOpen) Islands.Refresh();
        }

        void Status(string text, Color color)
        {
            _status.text = text;
            _status.color = color;
        }

        public async void Refresh()
        {
            var request = ++_requests;
            Status("Loading the market...", HackUi.Dim);
            try
            {
                var (market, error) = await IslandApi.Market();
                if (this == null || request != _requests) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                _market = market;
                _mine = null;
                _islands = null;
                if (Web3Session.Instance.SignedIn)
                {
                    (_mine, _) = await IslandApi.MyListings();
                    if (this == null || request != _requests) return;
                    (_islands, _) = await IslandApi.Mine();
                    if (this == null || request != _requests) return;
                }
                Status(Web3Session.Instance.SignedIn ? $"{market.listings?.Length ?? 0} {((market.listings?.Length ?? 0) == 1 ? "island" : "islands")} for sale. Escrow {IslandUi.Short(market.treasury)} (devnet)."
                    : "Connect a wallet (top of the menu) to buy or sell.", HackUi.Dim);
                Render();
            }
            catch (Exception e)
            {
                Debug.LogWarning("MarketPanel: " + e);
                if (this != null) Status("Could not load the market: " + e.Message, HackUi.Bad);
            }
        }

        void Render()
        {
            for (var i = _list.childCount - 1; i >= 0; i--)
            {
                var c = _list.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            var myId = Web3Session.Instance.SignedIn && Web3Session.Instance.Profile != null ? Web3Session.Instance.Profile.playerId : null;

            IslandUi.TextRow(_list, "<color=#ffd166><b>For sale</b></color>", 28);
            var any = false;
            if (_market?.listings != null)
                foreach (var l in _market.listings)
                {
                    if (l == null || l.status != "active") continue;
                    any = true;
                    var mine = myId != null && l.seller != null && l.seller.playerId == myId;
                    var row = HackUi.Row(_list, 112f, mine ? IslandUi.MineTint : IslandUi.RowTint);
                    IslandUi.Picture(row, ImageFor(l), 100f);
                    var isl = l.island ?? new ListingIsland();
                    var c = HackUi.Cell(row, $"<b>{isl.name}</b>   Level {isl.level}\n<size=82%><color=#9aa4ad>{isl.slotName}, {isl.region}   {isl.ratePerHour} RUM/h   seller {l.seller?.name} {l.seller?.walletShort}</color></size>",
                        0f, 26, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                    c.textWrappingMode = TextWrappingModes.Normal;
                    HackUi.Cell(row, $"<color=#ffd166><b>{HackathonRules.Sol(l.priceLamports)}</b></color>", 170f, 28, TextAlignmentOptions.MidlineRight, HackUi.TextColor);
                    var listing = l;
                    if (mine) IslandUi.RowButton(row, "Cancel", () => Cancel(listing), 180f);
                    else IslandUi.RowButton(row, "Buy", () => Buy(listing), 180f).interactable = Web3Session.Instance.SignedIn;
                }
            if (!any) IslandUi.TextRow(_list, "No islands for sale right now. List yours below!", 25, HackUi.Dim);

            // Sell.
            if (Web3Session.Instance.SignedIn)
            {
                IslandUi.TextRow(_list, "<color=#ffd166><b>Sell your island</b></color>", 28);
                var islands = _islands?.islands;
                IslandView sellable = null;
                if (islands != null) foreach (var i in islands) if (i != null && !i.listed) { sellable = i; break; }
                if (sellable == null)
                    IslandUi.TextRow(_list, islands == null || islands.Length == 0 ? "You have no island yet (claim one on the Islands screen)." : "Your island is already listed.", 25, HackUi.Dim);
                else
                {
                    var row = HackUi.Row(_list, 112f, IslandUi.RowTint);
                    IslandUi.Picture(row, sellable.nft?.imageUrl, 100f);
                    var c = HackUi.Cell(row, $"<b>{sellable.name}</b>   Level {sellable.level}\n<size=82%><color=#9aa4ad>NFT {IslandUi.Short(sellable.nft?.mint)}  " +
                                             (sellable.nft != null && sellable.nft.Minted ? "goes to the escrow until sold or cancelled" : "<color=#ff8c73>not minted yet</color>") + "</color></size>",
                        0f, 25, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                    c.textWrappingMode = TextWrappingModes.Normal;
                    _price = HackUi.Input(row, "Price SOL", 10);
                    var le = _price.GetComponent<LayoutElement>();
                    le.preferredWidth = le.minWidth = 180f;
                    le.flexibleWidth = 0f;
                    _price.text = "0.05";
                    var island = sellable;
                    IslandUi.RowButton(row, "List", () => List(island), 160f).interactable = sellable.nft != null && sellable.nft.Minted;
                }
            }

            // My listings (history and anything to finish).
            if (_mine?.listings != null && _mine.listings.Length > 0)
            {
                IslandUi.TextRow(_list, "<color=#ffd166><b>Your trades</b></color>", 28);
                foreach (var l in _mine.listings)
                {
                    if (l == null) continue;
                    var seller = myId != null && l.seller != null && l.seller.playerId == myId;
                    var row = HackUi.Row(_list, 84f, IslandUi.RowTint);
                    var color = l.status == "sold" ? "#7fd68b" : l.status == "active" ? "#ffd166" : l.status == "cancelled" ? "#9aa4ad" : "#ff8c73";
                    var c = HackUi.Cell(row, $"<b>{l.island?.name}</b>  {(seller ? (l.status == "sold" ? "you sold it" : "your listing") : "you bought it")}  {HackathonRules.Sol(l.priceLamports)}  <color={color}>{l.status}</color>" +
                                             (string.IsNullOrEmpty(l.error) ? string.Empty : $"\n<size=80%><color=#ff8c73>{l.error}</color></size>"),
                        0f, 24, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                    c.textWrappingMode = TextWrappingModes.Normal;
                    var listing = l;
                    if (l.status == "paid") IslandUi.RowButton(row, "Complete", () => Complete(listing), 180f);
                    else if (l.status == "cancelling" && seller) IslandUi.RowButton(row, "Cancel again", () => Cancel(listing), 200f);
                    else if (l.status == "active" && seller) IslandUi.RowButton(row, "Cancel", () => Cancel(listing), 160f);
                    var url = !string.IsNullOrEmpty(l.transferExplorerUrl) ? l.transferExplorerUrl : HackathonRules.ExplorerTx(l.escrowSignature);
                    if (!string.IsNullOrEmpty(url)) IslandUi.RowButton(row, "NFT tx", () => Application.OpenURL(url), 150f);
                    if (!string.IsNullOrEmpty(l.nftMint)) IslandUi.RowButton(row, "NFT", () => Application.OpenURL(IslandRules.ExplorerAddress(listing.nftMint)), 110f);
                }
            }
        }

        string ImageFor(Listing l)
        {
            var baseUrl = RoguePirates.V4.Online.OnlineService.Client?.BaseUrl;
            return l.island == null || string.IsNullOrEmpty(baseUrl) ? null : baseUrl.TrimEnd('/') + "/static/islands/" + l.island.slot + ".png";
        }

        async void Buy(Listing l)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var (done, error) = await IslandApi.Buy(l, s => { if (this != null) Status(s, HackUi.Gold); });
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status(done.status == "sold"
                    ? $"{done.island?.name} is yours! NFT {IslandUi.Short(done.nftMint)} moved to your wallet; the seller was paid."
                    : $"Paid. The trade is {done.status}" + (string.IsNullOrEmpty(done.error) ? "." : ": " + done.error + " Press Complete under Your trades."), done.status == "sold" ? HackUi.Good : HackUi.Gold);
                LastTrade = done;
            }
            finally { _busy = false; }
            AfterTrade();
        }

        /// <summary>The last buy, list or cancel (the screen's success line links its NFT).</summary>
        public Listing LastTrade { get; private set; }

        async void List(IslandView island)
        {
            if (_busy) return;
            var lamports = IslandRules.ParseSol(_price != null ? _price.text : null);
            if (lamports < 1_000_000L) { Status("Type a price in SOL (0.001 or more).", HackUi.Bad); return; }
            _busy = true;
            try
            {
                var (listing, error) = await IslandApi.List(island, lamports, s => { if (this != null) Status(s, HackUi.Gold); });
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status($"{island.name} is listed for {HackathonRules.Sol(listing.priceLamports)}. The NFT is in escrow ({HackathonRules.ShortSig(listing.escrowSignature)}).", HackUi.Good);
                LastTrade = listing;
            }
            finally { _busy = false; }
            AfterTrade();
        }

        async void Cancel(Listing l)
        {
            if (_busy) return;
            _busy = true;
            Status("Cancelling: the escrow sends the NFT back to your wallet...", HackUi.Gold);
            try
            {
                var (listing, error) = await IslandApi.Cancel(l.listingId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status(listing.status == "cancelled" ? $"Listing cancelled. {listing.island?.name}'s NFT is back in your wallet." : $"Cancel is {listing.status}: {listing.error} Press Cancel again.",
                    listing.status == "cancelled" ? HackUi.Good : HackUi.Bad);
                LastTrade = listing;
            }
            finally { _busy = false; }
            AfterTrade();
        }

        async void Complete(Listing l)
        {
            if (_busy) return;
            _busy = true;
            Status("Finishing the trade...", HackUi.Gold);
            try
            {
                var (listing, error) = await IslandApi.Complete(l.listingId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Status(listing.status == "sold" ? "Trade complete." : $"The trade is {listing.status}: {listing.error}", listing.status == "sold" ? HackUi.Good : HackUi.Bad);
            }
            finally { _busy = false; }
            AfterTrade();
        }

        async void AfterTrade()
        {
            var text = _status.text;
            var color = _status.color;
            var request = ++_requests;
            var (market, _) = await IslandApi.Market();
            if (this == null || request != _requests) return;
            if (market != null) _market = market;
            (_mine, _) = await IslandApi.MyListings();
            if (this == null || request != _requests) return;
            (_islands, _) = await IslandApi.Mine();
            if (this == null || request != _requests) return;
            Render();
            Status(text, color);
            _ = Web3Session.Instance.RefreshBalance();
        }
    }
}
