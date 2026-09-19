import { createRouter, createWebHistory } from 'vue-router';
import { getToken } from '../api';

const routes = [
  { path: '/login', component: () => import('../views/Login.vue'), meta: { blank: true } },
  {
    path: '/',
    component: () => import('../layouts/StudentLayout.vue'),
    children: [
      { path: '', component: () => import('../views/student/Home.vue') },
      { path: 'sim/lab', component: () => import('../views/student/SimLab.vue') },
      { path: 'resources/cases', component: () => import('../views/student/Cases.vue') },
      { path: 'resources/ideology', component: () => import('../views/student/Ideology.vue') },
      { path: 'resources/quiz', component: () => import('../views/student/Quiz.vue') },
      { path: 'resources/projects', component: () => import('../views/student/Projects.vue') },
      { path: 'workshop/cleaning', component: () => import('../views/student/Cleaning.vue') },
      { path: 'workshop/uncertainty', component: () => import('../views/student/Uncertainty.vue') },
      { path: 'workshop/analysis', component: () => import('../views/student/Analysis.vue') },
      { path: 'peer/works', component: () => import('../views/student/Works.vue') },
      { path: 'peer/appeals', component: () => import('../views/student/Appeals.vue') }
    ]
  },
  {
    path: '/teacher',
    component: () => import('../layouts/TeacherLayout.vue'),
    meta: { teacher: true },
    children: [
      { path: '', component: () => import('../views/teacher/Dashboard.vue') },
      { path: 'resources', component: () => import('../views/teacher/ResourceManager.vue') },
      { path: 'students', component: () => import('../views/teacher/Students.vue') },
      { path: 'pushes', component: () => import('../views/teacher/Pushes.vue') },
      { path: 'grades', component: () => import('../views/teacher/Grades.vue') },
      { path: 'appeals', component: () => import('../views/teacher/Appeals.vue') }
    ]
  }
];

const router = createRouter({ history: createWebHistory(), routes });
router.beforeEach((to) => {
  if (!getToken() && !to.meta.blank) return '/login';
  return true;
});
export default router;
