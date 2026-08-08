// Browser/device capability queries that Unity's WebGL sandbox can't answer
// on its own. Kept separate from EmmasWorldBridge.jslib, which is scoped to
// the multiplayer socket connection.
mergeInto(LibraryManager.library, {

  // navigator.maxTouchPoints reflects actual touch-digitizer hardware.
  // Input.touchSupported / 'ontouchstart' in window are unreliable for this --
  // many non-touch desktop browsers implement the Touch Events API surface
  // anyway and report support even without real touch hardware.
  EmmasWorld_IsTouchDevice: function () {
    var points = (typeof navigator !== 'undefined' && (navigator.maxTouchPoints || navigator.msMaxTouchPoints)) || 0;
    return points > 0 ? 1 : 0;
  },

});
