import { createRouter, createWebHashHistory } from 'vue-router'
import EnvironmentView from '../views/EnvironmentView.vue'
import ToolsView from '../views/ToolsView.vue'
import AddToolView from '../views/AddToolView.vue'
import SettingsView from '../views/SettingsView.vue'

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
      component: EnvironmentView,
    },
    {
      path: '/tools',
      name: 'tools',
      component: ToolsView,
    },
    {
      path: '/tools/new',
      name: 'addTool',
      component: AddToolView,
    },
    {
      path: '/settings',
      name: 'settings',
      component: SettingsView,
    },
  ],
})
