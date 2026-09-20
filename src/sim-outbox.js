import {api} from './api';
export function createOutbox(userId,onStatus){
  const key='hall-outbox:'+userId;let items=JSON.parse(localStorage.getItem(key)||'[]');let running=null;
  function persist(){localStorage.setItem(key,JSON.stringify(items));onStatus(items.length?'待保存 '+items.length+' 条':'已保存');}
  return {
    get pending(){return items.length;},
    enqueue(sessionId,event){if(!items.some(x=>x.event.eventId===event.eventId)){items.push({sessionId,event});persist();}},
    flush(){if(running)return running;running=(async()=>{while(items.length){const sid=items[0].sessionId;const batch=items.filter(x=>x.sessionId===sid).slice(0,100);const r=await api('/sim/events',{method:'POST',body:{platformSessionId:sid,events:batch.map(x=>x.event)}});const ack=new Set(r.ack||[]);if(!ack.size)throw Error('服务器未确认数据');items=items.filter(x=>x.sessionId!==sid||!ack.has(x.event.eventId));persist();}})().catch(e=>{onStatus('保存失败：'+e.message);throw e;}).finally(()=>running=null);return running;}
  };
}
