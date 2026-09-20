import { defineStore } from 'pinia';
import { api, getToken, setToken, clearToken } from '../api';

export const useAuth = defineStore('auth', {
  state: () => ({ token: getToken(), user: null }),
  getters: {
    isLogin: s => !!s.token,
    isTeacher: s => s.user?.role === 'teacher'
  },
  actions: {
    async login(username, password) {
      const r = await api('/auth/login', { method: 'POST', body: { username, password } });
      this.token = r.token; setToken(r.token);
      await this.fetchMe();
      return r;
    },
    async fetchMe() {
      this.token=getToken();
      if (!this.token){this.user=null;return;}
      this.user = await api('/me');
      return this.user;
    },
    async logout() {const token=getToken();try{await api('/auth/logout',{method:'POST'});}finally{if(clearToken(token)){this.token=null;this.user=null;sessionStorage.clear();location.href='/login';}} }
  }
});
