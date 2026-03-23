import { createRouter, createWebHashHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/',
      redirect: '/python',
    },
    {
      path: '/python',
      alias: '/workspace',
      name: 'pythonPackages',
      component: () => import('../views/EnvironmentView.vue'),
    },
    {
      path: '/tools',
      name: 'tools',
      component: () => import('../views/ToolsView.vue'),
    },
    {
      path: '/tools/new',
      name: 'addTool',
      component: () => import('../views/AddToolView.vue'),
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('../views/SettingsView.vue'),
    },
    {
      path: '/ai-settings',
      name: 'aiSettings',
      component: () => import('../views/AiSettingsView.vue'),
    },
  ],
})
