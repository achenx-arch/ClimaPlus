import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

/**
 * Fase 8 (OWASP A05/A03): Content-Security-Policy del frontend compilado.
 * Solo se aplica en `npm run build`: el servidor de desarrollo de Vite necesita scripts en línea para la recarga en
 * caliente. Limita los scripts al propio sitio (mitiga XSS), las conexiones a la API configurada (VITE_API_URL) y las
 * fuentes a Google Fonts. frame-ancestors no funciona en <meta>: se enviará como cabecera desde el servidor web (Fase 9).
 * Autenticación segura (fase 6): "Continuar con Google" (Google Identity Services) necesita exactamente los orígenes
 * que indica Google: el script gsi/client, su hoja de estilos, el iframe del botón y sus conexiones, todos bajo
 * https://accounts.google.com/gsi/. No se abre ningún otro origen.
 */
const GOOGLE_GSI = 'https://accounts.google.com/gsi/'

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
            `script-src 'self' ${GOOGLE_GSI}client`,
            `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com ${GOOGLE_GSI}style`,
            "font-src 'self' https://fonts.gstatic.com",
            "img-src 'self' data:",
            `connect-src 'self' ${origenApi} ${GOOGLE_GSI}`,
            `frame-src ${GOOGLE_GSI}`,
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
