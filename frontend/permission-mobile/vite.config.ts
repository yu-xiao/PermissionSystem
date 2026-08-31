import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5174,
      strictPort: true,
      // Keep the API origin configurable while allowing local H5 development.
      proxy: env.VITE_API_BASE_URL
        ? undefined
        : {
            '/api': { target: 'https://localhost:7281', secure: false },
            '/connect': { target: 'https://localhost:7281', secure: false },
            '/hubs': { target: 'https://localhost:7281', secure: false, ws: true },
          },
    },
    build: {
      sourcemap: mode !== 'production',
      rollupOptions: {
        output: {
          assetFileNames: 'assets/[name]-[hash][extname]',
          chunkFileNames: 'assets/[name]-[hash].js',
          entryFileNames: 'assets/[name]-[hash].js',
        },
      },
    },
  }
})
