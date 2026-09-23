(function () {
  'use strict';
  let instance, ready = false;
  const allowed = new Set(['InitExperiment','SetParameter','GotoStep','ResetExperiment','FinishFromWeb','RequestSnapshot','RecordMeasurement','ConnectTerminals','UndoLastCable']);
  const journal = [];
  let storageError = null;
  let previousSession = null;
  try { previousSession = localStorage.getItem('hall.lastSession'); } catch(error) { storageError=String(error); }
  window.HallHost = {
    attach(value) { instance = value; announce(); },
    get ready() { return ready && !!instance; },
    get events() { return journal.slice(); },
    get storageError() { return storageError; },
    get previousSession() { return previousSession; },
    send(method, payload) {
      if (!this.ready) throw new Error('Unity is not ready');
      if (!allowed.has(method)) throw new Error('Unknown method');
      instance.SendMessage('WebBridge', method, payload === undefined ? '' : typeof payload === 'string' ? payload : JSON.stringify(payload));
    },
    export() {
      const blob = new Blob([JSON.stringify({schemaVersion:1,events:journal},null,2)], {type:'application/json'});
      const url=URL.createObjectURL(blob), a=document.createElement('a');
      a.href=url;a.download='hall-session-'+new Date().toISOString().replace(/[:.]/g,'-')+'.json';a.click();
      setTimeout(()=>URL.revokeObjectURL(url),1000);
    }
  };
  function announce() {
    if (ready && instance) {
      window.dispatchEvent(new Event('hall-ready'));
      if (parent!==window) parent.postMessage({source:'hall-host',ready:true},location.origin);
    }
  }
  window.addEventListener('hall-event', ({detail:event}) => {
    if(event.type==='OnReady'){ready=true;announce();}
    // A directly opened /sim/index.html has no Vue parent to consume the
    // Unity exit event. Return to the platform shell instead of leaving the
    // button apparently inert; iframe usage still delegates to SimLab below.
    if(event.type==='OnExitRequested' && parent===window){
      try { localStorage.setItem('hall.lastSession',JSON.stringify({schemaVersion:1,events:journal.concat(event)})); } catch {}
      window.location.assign('/');
      return;
    }
    if(parent!==window){ try{ parent.postMessage({source:'hall-unity',event},location.origin); }catch(e){} }
    if(event.isDemo)return;
    if(event.type==='OnSnapshot')return;
    journal.push(event);
    // Local recovery is best effort. Production integrations must acknowledge server persistence.
    try { localStorage.setItem('hall.lastSession',JSON.stringify({schemaVersion:1,events:journal}));storageError=null; }
    catch(error) {storageError=String(error);window.dispatchEvent(new CustomEvent('hall-storage-error',{detail:storageError}));}
  });
  window.addEventListener('message',event=>{
    if(event.origin!==location.origin||event.source!==parent||event.data?.source!=='hall-platform')return;
    const {method,payload,requestId}=event.data;
    try {window.HallHost.send(method,payload);parent.postMessage({source:'hall-host',requestId,dispatched:true},location.origin);}
    catch(error){parent.postMessage({source:'hall-host',requestId,error:String(error)},location.origin);}
  });
  window.addEventListener('beforeunload',event=>{
    const last=journal[journal.length-1];
    if(last && !last.state?.finished && journal.some(e=>e.type==='OnMeasureData')){event.preventDefault();event.returnValue='';}
  });
})();
