// V5 M9b playtest fixes (E): whether the page is hidden (another tab, a minimised window). The server voyage's client
// polls it each frame; a hidden page tells the match server it is away, so a solo voyage holds as the normal game stops.
mergeInto(LibraryManager.library, {
  RP_NetPageHidden: function () {
    return (typeof document !== 'undefined' && document.hidden) ? 1 : 0;
  }
});
