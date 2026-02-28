import { createRouter, createWebHashHistory } from 'vue-router'
import EnvironmentView from '../views/EnvironmentView.vue'
import ToolsView from '../views/ToolsView.vue'
import SiliconFlowView from '../views/SiliconFlowView.vue'

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
      path: '/siliconflow',
      name: 'siliconflow',
      component: SiliconFlowView,
    },
  ],
})
