import { createRouter, createWebHistory } from 'vue-router';
import { getToken } from '../api';
import {api} from '../api';

const routes = [
  { path: '/login', component: () => import('../views/Login.vue'), meta: { blank: true } },
  {
    path: '/',
    component: () => import('../layouts/StudentLayout.vue'),
    meta: { student: true },
    children: [
      { path: '', component: () => import('../views/student/Home.vue') },
      { path: 'sim/lab', component: () => import('../views/student/SimLab.vue') },
      { path: 'sim/side-effects', component: () => import('../views/student/SideEffects.vue') },
      { path: 'resources/cases', component: () => import('../views/student/Cases.vue') },
      { path: 'resources/ideology', component: () => import('../views/student/Ideology.vue') },
      { path: 'resources/quiz', component: () => import('../views/student/Quiz.vue') },
      { path: 'resources/projects', component: () => import('../views/student/Projects.vue') },
      { path: 'resources/downloads', component: () => import('../views/student/Downloads.vue') },
      { path: 'workshop/cleaning', component: () => import('../views/student/Cleaning.vue') },
      { path: 'workshop/uncertainty', component: () => import('../views/student/Uncertainty.vue') },
      { path: 'workshop/analysis', component: () => import('../views/student/Analysis.vue') },
      { path: 'peer/works', component: () => import('../views/student/Works.vue') },
      { path: 'peer/appeals', component: () => import('../views/student/Appeals.vue') }
      ,{path:'grades',component:()=>import('../views/student/Grades.vue')}
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
      ,{path:'review',component:()=>import('../views/teacher/Review.vue')}
      ,{path:'rubrics',component:()=>import('../views/teacher/Rubrics.vue')}
      ,{path:'side-effects',component:()=>import('../views/teacher/SideEffectMonitor.vue')}
      ,{path:'side-effects/:studentId',component:()=>import('../views/teacher/SideEffectDetail.vue')}
    ]
  },{path:'/:pathMatch(.*)*',redirect:'/'}
];

const router = createRouter({ history: createWebHistory(), routes });
router.beforeEach(async(to) => {
  if (!getToken() && !to.meta.blank) return '/login';
  if(!to.meta.blank){try{const me=await api('/me');if(to.meta.teacher&&me.role!=='teacher')return '/';if(to.meta.student&&me.role==='teacher')return '/teacher';}catch{return '/login';}}
  return true;
});
export default router;
