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
      if (!this.token) return;
      this.user = await api('/me');
    },
    logout() { this.token = null; this.user = null; clearToken(); location.href = '/login'; }
  }
});
