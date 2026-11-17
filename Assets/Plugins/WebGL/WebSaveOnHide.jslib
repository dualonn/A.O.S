// Browser builds only: save the game when the tab is hidden or about to close (a browser gives the
// game no quit message). Calls Settlement.SaveNow through Unity's SendMessage.
mergeInto(LibraryManager.library, {
  WebSaveOnHide: function (objectNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var save = function () {
      try {
        if (typeof SendMessage === "function") SendMessage(objectName, "SaveNow");
        else if (typeof Module !== "undefined" && Module.SendMessage) Module.SendMessage(objectName, "SaveNow");
      } catch (e) { }
    };
    document.addEventListener("visibilitychange", function () {
      if (document.visibilityState === "hidden") save();
    });
    window.addEventListener("pagehide", save);
  }
});
