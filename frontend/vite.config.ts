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
        manualChunks(id) {
          const normalizedId = id.replace(/\\/g, '/')

          if (normalizedId.includes('/node_modules/vue/')
            || normalizedId.includes('/node_modules/vue-router/')
            || normalizedId.includes('/node_modules/pinia/')) {
            return 'vue-vendor'
          }

          if (normalizedId.includes('/node_modules/@xterm/')) {
            return 'xterm'
          }

          if (normalizedId.includes('/node_modules/vue-devui/notification/')) {
            return 'devui-notification'
          }

          if (normalizedId.includes('/node_modules/clipboard/')) {
            return 'clipboard'
          }

          if (normalizedId.includes('/node_modules/lodash/')) {
            return 'lodash'
          }
        },
      },
    },
  },
})
