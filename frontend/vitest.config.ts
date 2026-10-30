import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config.ts'

// Pruebas del frontend (Vitest + Testing Library + jsdom). No llaman al backend real:
// fetch se sustituye en cada prueba con respuestas tomadas de los contratos de docs/api.
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      env: { VITE_API_URL: 'http://localhost:5208' },
      restoreMocks: true,
    },
  }),
)
