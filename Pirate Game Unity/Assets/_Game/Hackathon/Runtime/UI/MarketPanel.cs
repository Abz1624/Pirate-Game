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
    /// 13 Oct: redrawn in the art team's parchment kit after the owner's reference (Pirate Island Market Interface): a
    /// wood header (skull medallion, title, wallet balance, Refresh, Close) over a parchment card with the intro, the
    /// status line and the list (For sale, Sell your island, Your trades) as inset cards.
    /// </summary>
    public sealed class MarketPanel : MonoBehaviour
    {
        const float HeaderHeight = 130f;

        RectTransform _list;
        TextMeshProUGUI _status, _sub, _balance;
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
            var rt = HackUi.Panel(canvas, "HackathonMarket", new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), 228);
            rt.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var panel = rt.gameObject.AddComponent<MarketPanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var card = MarketKit.Fill(rt, "Card", 8f, 0f, 8f, HeaderHeight - 40f);
            MarketKit.Paint(card, "Panel_Card", 1.2f);

            var head = MarketKit.Strip(rt, "Header", 0f, 0f, 0f, HeaderHeight);
            MarketKit.Header(head, "Island market", "Icon_Skull_Crossbones", 60f, 150f, 940f);
            _close = MarketKit.KitButton(MarketKit.TopRight(head, "Close", 24f, 22f, 230f, 86f), "Close", "Icon_Close", Close, "Btn_Gold", 34f, 38f);
            MarketKit.KitButton(MarketKit.TopRight(head, "Refresh", 270f, 22f, 230f, 86f), "Refresh", "Icon_Refresh", Refresh, "Btn_Gold", 34f, 38f);
            var bal = MarketKit.TopRight(head, "Balance", 520f, 24f, 400f, 82f);
            MarketKit.Icon(MarketKit.TopLeft(bal, "Chest", 0f, 4f, 74f, 74f), "Chest_2");
            _balance = MarketKit.Label(MarketKit.Fill(bal, "Text", 86f, 0f, 0f, 0f), string.Empty, 34f, MarketKit.OnWood, TextAlignmentOptions.MidlineLeft, true, 22f);

            _sub = MarketKit.Label(MarketKit.Strip(card, "Intro", 56f, 58f, 56f, 46f), "Buy and sell island NFTs for devnet SOL. The game's escrow holds a listed NFT until it sells.",
                30f, MarketKit.Ink, TextAlignmentOptions.MidlineLeft, false, 20f);
            _status = MarketKit.StatusLine(MarketKit.Strip(card, "Status", 50f, 108f, 50f, 58f), 27f);
            var host = MarketKit.Fill(card, "List", 46f, 34f, 40f, 180f);
            _list = MarketKit.Scroll(host, 14f, new RectOffset(4, 16, 4, 10));
        }

        void ShowBalance()
        {
            if (_balance == null) return;
            var s = Web3Session.Instance;
            _balance.text = string.IsNullOrEmpty(s.Address) ? "No wallet" : Web3Rules.FormatSol(s.Balance);
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

        /// <summary>The status line; callers pass <see cref="HackUi"/>'s colours, shown in the matching parchment ink.</summary>
        void Status(string text, Color color)
        {
            _status.text = text;
            _status.color = MarketKit.InkFor(color);
        }

        public async void Refresh()
        {
            var request = ++_requests;
            Status("Loading the market...", HackUi.Dim);
            ShowBalance();
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
            MarketKit.Clear(_list);
            ShowBalance();
            var myId = Web3Session.Instance.SignedIn && Web3Session.Instance.Profile != null ? Web3Session.Instance.Profile.playerId : null;
            var dim = MarketKit.Hex(MarketKit.InkDim);

            MarketKit.Section(_list, "Icon_Tag", "For sale", "RUM per hour and defence record help you judge an island");
            var any = false;
            if (_market?.listings != null)
                foreach (var l in _market.listings)
                {
                    if (l == null || l.status != "active") continue;
                    any = true;
                    var mine = myId != null && l.seller != null && l.seller.playerId == myId;
                    var row = MarketKit.Card(_list, "Listing", 176f, mine ? new Color(1f, 0.9f, 0.62f, 1f) : (Color?)null);
                    Picture(row, ImageFor(l), 144f);
                    var isl = l.island ?? new ListingIsland();
                    var info = Info(row, isl.name, isl.level, mine ? "your listing" : null);
                    IconLine(info, 64f, "Chest_2", $"<color={MarketKit.Hex(MarketKit.InkGold)}>{isl.ratePerHour} RUM / hr</color>", 32f);
                    var details = MarketKit.Strip(info, "Details", 0f, 110f, 0f, 40f);
                    MarketKit.Row(details, 10f);
                    IconText(details, "Icon_Shield", $"Defence: <color={RecordColor(isl.defenceRecord)}>{IslandRules.DefenceRecordLine(isl.defenceRecord)}</color>");
                    IconText(details, "Icon_Pin", $"{isl.slotName}, {isl.region}");
                    IconText(details, "Icon_User", $"{l.seller?.name} <color={dim}>{l.seller?.walletShort}</color>");
                    MarketKit.Chip(MarketKit.Cell(row, "Price", 230f, 92f), HackathonRules.Sol(l.priceLamports), 38f, MarketKit.Ink, true);
                    var listing = l;
                    if (mine) MarketKit.RowButton(row, "Cancel", null, () => Cancel(listing), 220f, 92f, "Btn_Red", 36f);
                    else MarketKit.RowButton(row, "Buy", null, () => Buy(listing), 220f, 92f, "Btn_Gold", 38f).interactable = Web3Session.Instance.SignedIn;
                }
            if (!any) MarketKit.Note(_list, "No islands for sale right now. List yours below!", 28f);

            // Sell.
            if (Web3Session.Instance.SignedIn)
            {
                MarketKit.Section(_list, "Icon_Upload", "Sell your island", "Listed NFTs stay in escrow until sold or cancelled.");
                var islands = _islands?.islands;
                IslandView sellable = null;
                if (islands != null) foreach (var i in islands) if (i != null && !i.listed) { sellable = i; break; }
                if (sellable == null)
                    MarketKit.Note(_list, islands == null || islands.Length == 0 ? "You have no island yet (claim one on the Islands screen)." : "Your island is already listed.", 28f);
                else
                {
                    var row = MarketKit.Card(_list, "Sell", 160f);
                    Picture(row, sellable.nft?.imageUrl, 130f);
                    var info = Info(row, sellable.name, sellable.level, null);
                    var minted = sellable.nft != null && sellable.nft.Minted;
                    var line = MarketKit.Label(MarketKit.Strip(info, "Nft", 0f, 70f, 0f, 40f),
                        $"NFT {IslandUi.Short(sellable.nft?.mint)}" + (minted ? string.Empty : $"   <color={MarketKit.Hex(MarketKit.InkBad)}>not minted yet</color>"),
                        28f, MarketKit.InkDim, TextAlignmentOptions.MidlineLeft, false, 18f);
                    line.richText = true;
                    _price = MarketKit.Input(MarketKit.Cell(row, "Price", 210f, 86f), "Price SOL", 10, 34f);
                    _price.contentType = TMP_InputField.ContentType.DecimalNumber;
                    _price.text = "0.05";
                    var island = sellable;
                    MarketKit.RowButton(row, "List", null, () => List(island), 220f, 92f, "Btn_Gold", 38f).interactable = minted;
                }
            }

            // My listings (history and anything to finish).
            if (_mine?.listings != null && _mine.listings.Length > 0)
            {
                MarketKit.Section(_list, "Icon_Clock", "Your trades");
                foreach (var l in _mine.listings)
                {
                    if (l == null) continue;
                    var seller = myId != null && l.seller != null && l.seller.playerId == myId;
                    var row = MarketKit.Card(_list, "Trade", 104f);
                    var color = l.status == "sold" ? MarketKit.InkGood : l.status == "active" ? MarketKit.InkGold : l.status == "cancelled" ? MarketKit.InkDim : MarketKit.InkBad;
                    var c = MarketKit.Label(MarketKit.Cell(row, "Text", 0f, 80f),
                        $"<b>{l.island?.name}</b>   <color={dim}>{(seller ? (l.status == "sold" ? "you sold it" : "your listing") : "you bought it")}</color>   {HackathonRules.Sol(l.priceLamports)}   <color={MarketKit.Hex(color)}><b>{l.status}</b></color>" +
                        (string.IsNullOrEmpty(l.error) ? string.Empty : $"\n<size=80%><color={MarketKit.Hex(MarketKit.InkBad)}>{l.error}</color></size>"),
                        28f, MarketKit.Ink, TextAlignmentOptions.MidlineLeft, false, 18f);
                    c.textWrappingMode = TextWrappingModes.Normal;
                    var listing = l;
                    if (l.status == "paid") MarketKit.RowButton(row, "Complete", null, () => Complete(listing), 210f, 76f, "Btn_Gold", 30f);
                    else if (l.status == "cancelling" && seller) MarketKit.RowButton(row, "Cancel again", null, () => Cancel(listing), 240f, 76f, "Btn_Red", 30f);
                    else if (l.status == "active" && seller) MarketKit.RowButton(row, "Cancel", null, () => Cancel(listing), 190f, 76f, "Btn_Red", 30f);
                    var url = !string.IsNullOrEmpty(l.transferExplorerUrl) ? l.transferExplorerUrl : HackathonRules.ExplorerTx(l.escrowSignature);
                    if (!string.IsNullOrEmpty(url)) MarketKit.RowButton(row, "NFT tx", "Icon_Link", () => Application.OpenURL(url), 200f, 76f, "Btn_Parchment", 28f);
                    if (!string.IsNullOrEmpty(l.nftMint)) MarketKit.RowButton(row, "NFT", "Icon_Link", () => Application.OpenURL(IslandRules.ExplorerAddress(listing.nftMint)), 160f, 76f, "Btn_Parchment", 28f);
                }
            }
        }

        /// <summary>An island picture in a card, on a kit slot frame.</summary>
        static void Picture(RectTransform row, string url, float size)
        {
            var frame = MarketKit.Cell(row, "Frame", size, size);
            MarketKit.Paint(frame, "Slot_Card", 2.4f, new Color(0.2f, 0.45f, 0.7f, 1f), new Color(0.15f, 0.4f, 0.65f, 1f));
            var pic = IslandUi.Picture(frame, url, size - 12f);
            pic.GetComponent<LayoutElement>().ignoreLayout = true;
            var prt = (RectTransform)pic.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(6f, 6f);
            prt.offsetMax = new Vector2(-6f, -6f);
        }

        /// <summary>The flexible middle of a card: the island's name with its level chip on top; returns the area.</summary>
        static RectTransform Info(RectTransform row, string name, int level, string note)
        {
            var info = MarketKit.Cell(row, "Info", 0f, 150f);
            var top = MarketKit.Strip(info, "Name", 0f, 4f, 0f, 58f);
            MarketKit.Row(top, 22f);
            var n = MarketKit.Label(MarketKit.Node(top, "Text"), name, 44f, MarketKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            var nle = n.gameObject.AddComponent<LayoutElement>();
            nle.flexibleWidth = 0f;
            nle.minHeight = 58f;
            MarketKit.Chip(MarketKit.Cell(top, "Level", 112f, 50f), "Lv " + level, 30f);
            if (!string.IsNullOrEmpty(note)) MarketKit.Chip(MarketKit.Cell(top, "Note", 190f, 50f), note, 26f, MarketKit.InkGold);
            return info;
        }

        static void IconLine(RectTransform info, float y, string icon, string text, float size)
        {
            var line = MarketKit.Strip(info, "Line", 0f, y, 0f, 44f);
            MarketKit.Icon(MarketKit.TopLeft(line, "Icon", 0f, 2f, 40f, 40f), icon);
            MarketKit.Label(MarketKit.Fill(line, "Text", 52f, 0f, 0f, 0f), text, size, MarketKit.Ink, TextAlignmentOptions.MidlineLeft, false, size * 0.6f);
        }

        static void IconText(RectTransform row, string icon, string text)
        {
            MarketKit.Icon(MarketKit.Cell(row, "Icon", 32f, 32f), icon, MarketKit.InkDim);
            var t = MarketKit.Label(MarketKit.Node(row, "Text"), text, 26f, MarketKit.InkDim, TextAlignmentOptions.MidlineLeft);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0f;
            MarketKit.Cell(row, "Gap", 16f);
        }

        /// <summary>Green ink for a strong record (75% and up), amber in between, red under 40%, dim with no raids.</summary>
        static string RecordColor(DefenceRecord r)
        {
            if (r == null || r.raidsAgainst <= 0) return MarketKit.Hex(MarketKit.InkDim);
            var p = IslandRules.SuccessPercent(r.defended, r.raidsAgainst) ?? 0;
            return MarketKit.Hex(p >= 75 ? MarketKit.InkGood : p >= 40 ? MarketKit.InkGold : MarketKit.InkBad);
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
