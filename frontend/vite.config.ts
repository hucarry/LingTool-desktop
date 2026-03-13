import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
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
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
