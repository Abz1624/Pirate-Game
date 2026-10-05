// Playtest (6 Oct, owner): a recorded voyage (Telemetry/SessionFile.cs) is handed to the player as a file download, so
// the balance model can be compared with it (plan/playtest/montecarlo/compare_session.mjs).
mergeInto(LibraryManager.library, {
  RPSession_Download: function (namePtr, textPtr) {
    var name = UTF8ToString(namePtr);
    var text = UTF8ToString(textPtr);
    try {
      var blob = new Blob([text], { type: "application/json" });
      var url = URL.createObjectURL(blob);
      var a = document.createElement("a");
      a.href = url;
      a.download = name;
      a.style.display = "none";
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
    } catch (e) {
      console.warn("Session download failed: " + e);
    }
  }
});
