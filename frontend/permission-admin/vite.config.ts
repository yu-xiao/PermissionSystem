import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [vue()],
  build: {
    sourcemap: mode !== 'production',
    rollupOptions: {
      output: {
        assetFileNames: 'assets/[name]-[hash][extname]',
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js',
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return undefined
          }

          if (id.includes('element-plus')) {
            return 'vendor-element-plus'
          }

          if (id.includes('/vue/') || id.includes('pinia') || id.includes('vue-router')) {
            return 'vendor-vue'
          }

          if (id.includes('axios') || id.includes('nprogress')) {
            return 'vendor-http'
          }

          return 'vendor'
        },
      },
    },
  },
}))
