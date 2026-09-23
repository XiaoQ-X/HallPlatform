mergeInto(LibraryManager.library, {
  HallWebReport: function(namePtr, jsonPtr) {
    var name = UTF8ToString(namePtr);
    var payload = JSON.parse(UTF8ToString(jsonPtr));
    // A fixed event channel works standalone and in a same-origin iframe.
    window.dispatchEvent(new CustomEvent('hall-event', {detail: payload}));
    if (window.parent !== window) window.parent.postMessage({source:'hall-unity',event:payload}, window.location.origin);
    if (typeof window[name] === 'function') {
      try { window[name](payload); } catch (error) { console.error('Hall host callback failed', error); }
    }
  }
});
