import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  base: './',
  build: {
    outDir: '../ToolHub.App/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          // Vue 核心运行时
          'vue-vendor': ['vue', 'vue-router'],
          // DevUI 组件库
          'devui': ['vue-devui'],
          // xterm 终端
          'xterm': ['@xterm/xterm', '@xterm/addon-fit'],
        },
      },
    },
  },
})
