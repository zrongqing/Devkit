import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_DEV_')
  const port = Number.parseInt(env.VITE_DEV_PORT || '5173', 10)

  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error('VITE_DEV_PORT 必须是 1 到 65535 之间的整数')
  }

  return {
    plugins: [vue()],
    server: {
      host: env.VITE_DEV_HOST || '127.0.0.1',
      port,
      strictPort: true,
      open: env.VITE_DEV_OPEN === 'false' ? false : env.VITE_DEV_OPEN || '/',
    },
  }
})
