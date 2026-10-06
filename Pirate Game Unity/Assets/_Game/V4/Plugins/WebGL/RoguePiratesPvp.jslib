// Playtest (3 Oct, owner): the 1v1 lobby WebSocket for WebGL builds (System.Net.WebSockets does not run in the browser),
// the clipboard for invite links, and dropping ?join= from the address bar once read. The C# side polls each socket's
// queued events once a frame (no callbacks), so everything is handled on the main thread.
var RoguePiratesPvp = {
  $rpPvp: { next: 1, sockets: {} },

  RPPvp_SocketOpen: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = rpPvp.next++;
    var s = { ws: null, queue: [] };
    rpPvp.sockets[id] = s;
    try {
      s.ws = new WebSocket(url);
    } catch (e) {
      s.queue.push("C0 " + (e && e.message ? e.message : "connect failed"));
      return id;
    }
    s.ws.onopen = function () { s.queue.push("O"); };
    s.ws.onmessage = function (e) { if (typeof e.data === "string") s.queue.push("M" + e.data); };
    s.ws.onclose = function (e) { s.queue.push("C" + (e.code || 0) + " " + (e.reason || "")); };
    s.ws.onerror = function () { };
    return id;
  },

  RPPvp_SocketSend: function (id, textPtr) {
    var s = rpPvp.sockets[id];
    if (!s || !s.ws || s.ws.readyState !== 1) return 0;
    s.ws.send(UTF8ToString(textPtr));
    return 1;
  },

  RPPvp_SocketClose: function (id) {
    var s = rpPvp.sockets[id];
    if (!s) return;
    try { if (s.ws) s.ws.close(1000, "bye"); } catch (e) { }
    delete rpPvp.sockets[id];
  },

  RPPvp_SocketPoll: function (id) {
    var s = rpPvp.sockets[id];
    if (!s || s.queue.length === 0) return null;
    var text = s.queue.shift();
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  RPPvp_CopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(text); return; }
    } catch (e) { }
    try {
      var area = document.createElement("textarea");
      area.value = text;
      area.style.position = "fixed";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.select();
      document.execCommand("copy");
      document.body.removeChild(area);
    } catch (e) { }
  },

  RPPvp_DropJoinFromUrl: function () {
    try {
      var url = new URL(window.location.href);
      if (!url.searchParams.has("join")) return;
      url.searchParams.delete("join");
      window.history.replaceState(null, "", url.toString());
    } catch (e) { }
  }
};

autoAddDeps(RoguePiratesPvp, "$rpPvp");
mergeInto(LibraryManager.library, RoguePiratesPvp);
