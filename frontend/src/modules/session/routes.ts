import type { RouteRecordRaw } from 'vue-router'

export const sessionRoutes: RouteRecordRaw[] = [
  {
    path: '',
    name: 'home',
    component: () => import('./views/SessionView.vue'),
    meta: { title: 'Inicio' },
  },
]
