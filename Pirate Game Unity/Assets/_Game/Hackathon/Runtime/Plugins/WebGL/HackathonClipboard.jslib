// Hackathon (12 Oct): writes text to the browser clipboard for the wallet panel's "Copy address" button (Unity's
// GUIUtility.systemCopyBuffer stays inside the player on WebGL). Uses the async Clipboard API, with the old
// execCommand("copy") fallback for pages without it.
mergeInto(LibraryManager.library, {
  HackathonCopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);
    function fallback() {
      try {
        var area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.left = "-9999px";
        document.body.appendChild(area);
        area.select();
        document.execCommand("copy");
        document.body.removeChild(area);
      } catch (e) { console.warn("Copy failed: " + e); }
    }
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(fallback);
      else fallback();
    } catch (e) { fallback(); }
  }
});
