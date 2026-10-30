import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

/**
 * Fase 8 (OWASP A05/A03): Content-Security-Policy del frontend compilado.
 * Solo se aplica en `npm run build`: el servidor de desarrollo de Vite necesita scripts en línea para la recarga en
 * caliente. Limita los scripts al propio sitio (mitiga XSS), las conexiones a la API configurada (VITE_API_URL) y las
 * fuentes a Google Fonts. frame-ancestors no funciona en <meta>: se enviará como cabecera desde el servidor web (Fase 9).
 */
function cspProduccion(): Plugin {
  let origenApi = 'http://localhost:5208'

  return {
    name: 'climaplus-csp',
    apply: 'build',
    configResolved(config) {
      origenApi = new URL(config.env.VITE_API_URL || 'http://localhost:5208').origin
    },
    transformIndexHtml: () => [
      {
        tag: 'meta',
        attrs: {
          'http-equiv': 'Content-Security-Policy',
          content: [
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' https://fonts.gstatic.com",
            "img-src 'self' data:",
            `connect-src 'self' ${origenApi}`,
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
          ].join('; '),
        },
        injectTo: 'head-prepend',
      },
    ],
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), cspProduccion()],
})
