const TOKEN_KEY = 'hall_token';
let activeToken = localStorage.getItem(TOKEN_KEY);
let changingIdentity = false;
function refreshIdentity(){
  if(changingIdentity)return;
  changingIdentity=true;
  sessionStorage.clear();
  location.replace(localStorage.getItem(TOKEN_KEY)?'/':'/login');
}
// Bind this document to its original identity until a full reload clears its state.
export const getToken = () => {
  if(changingIdentity||localStorage.getItem(TOKEN_KEY)!==activeToken){refreshIdentity();throw Error('登录账号已变更，正在刷新页面');}
  return activeToken;
};
export const setToken = t => {activeToken=t;localStorage.setItem(TOKEN_KEY,t);};
export const clearToken = (expected=activeToken) => {if(localStorage.getItem(TOKEN_KEY)!==expected){refreshIdentity();return false;}activeToken=null;localStorage.removeItem(TOKEN_KEY);return true;};
window.addEventListener('storage',e=>{if((e.key===TOKEN_KEY||e.key===null)&&localStorage.getItem(TOKEN_KEY)!==activeToken)refreshIdentity();});
window.addEventListener('pageshow',()=>{if(localStorage.getItem(TOKEN_KEY)!==activeToken)refreshIdentity();});

const pending=new Map();
export function api(url,options={}){
  const mutation=options.method&&options.method!=='GET';const key=getToken()+url+JSON.stringify(options);
  if(mutation&&pending.has(key))return pending.get(key);
  const promise=request(url,options).catch(e=>{window.dispatchEvent(new CustomEvent('hall-error',{detail:e.message}));throw e;}).finally(()=>pending.delete(key));
  if(mutation)pending.set(key,promise);return promise;
}
async function request(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  const token = getToken();
  if (token) headers.Authorization = 'Bearer ' + token;
  let body = options.body;
  if (body && !(body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(body);
  }
  const res = await fetch('/api' + url, { ...options, headers, body,signal:options.signal||AbortSignal.timeout(30000) });
  if(token!==getToken())throw Error('登录账号已变更，请重新操作');
  if (res.status === 401&&url!=='/auth/login') { clearToken(); location.href = '/login'; throw new Error('未登录'); }
  const data = await res.json().catch(() => ({}));
  if(token!==getToken())throw Error('登录账号已变更，请重新操作');
  if (!res.ok) throw new Error(data.error || '请求失败');
  return data;
}

export async function upload(file, onProgress) {
  if(!file)throw new Error('请选择文件');
  const token=getToken();
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    const fd = new FormData();
    fd.append('file', file);
    xhr.open('POST', '/api/upload');
    xhr.setRequestHeader('Authorization', 'Bearer ' + token);
    xhr.upload.onprogress = e => onProgress && onProgress(e.loaded / e.total);
    xhr.onload = () => {
      try{if(token!==getToken())throw Error('登录账号已变更，请重新上传');}catch(e){reject(e);return;}
      if (xhr.status === 200) resolve(JSON.parse(xhr.responseText));
      else {let message='上传失败';try{message=JSON.parse(xhr.responseText).error||message;}catch{}window.dispatchEvent(new CustomEvent('hall-error',{detail:message}));reject(new Error(message));}
    };
    xhr.onerror = () => reject(new Error('上传失败'));
    xhr.send(fd);
  });
}
