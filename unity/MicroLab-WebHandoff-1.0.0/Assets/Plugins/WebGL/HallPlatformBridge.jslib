// 霍尔平台 WebGL 通信插件：把 Unity 内的事件以 hall-event 形式派发给页面的 hall-host.js，
// 再由 hall-host 转发给教学平台。平台下行命令通过 UnityLoader 的 SendMessage 调用 WebBridge。
mergeInto(LibraryManager.library, {
  HallPlatformEmit: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    if (typeof window === 'undefined') return;
    try {
      var event = JSON.parse(json);
      window.dispatchEvent(new CustomEvent('hall-event', { detail: event }));
    } catch (e) {}
  }
});
