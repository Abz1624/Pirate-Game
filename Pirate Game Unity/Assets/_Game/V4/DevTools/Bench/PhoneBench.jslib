// V5 phone benchmark: the size of the WebAssembly heap (the memory a browser tab grows for the game).
mergeInto(LibraryManager.library, {
  V5BenchHeapBytes: function () {
    return HEAPU8.length;
  }
});
