// Carga de Google Identity Services (GIS) para el botón "Continuar con Google".
// - Script oficial: https://accounts.google.com/gsi/client (permitido en la CSP de vite.config.ts).
// - Solo se usan initialize y renderButton (popup). Sin Client Secret ni tokens de acceso: GIS entrega un ID token
//   ("credential") que se envía a la API, que es quien lo valida.
// - Nunca se escribe el ID token en la consola.

export const URL_SCRIPT_GOOGLE = "https://accounts.google.com/gsi/client";

export interface RespuestaCredencialGoogle {
  credential: string;
}

export interface ConfiguracionGoogle {
  client_id: string;
  nonce: string;
  callback: (respuesta: RespuestaCredencialGoogle) => void;
  auto_select?: boolean;
  cancel_on_tap_outside?: boolean;
  ux_mode?: "popup" | "redirect";
  use_fedcm_for_button?: boolean;
}

export interface OpcionesBotonGoogle {
  type?: "standard" | "icon";
  theme?: "outline" | "filled_blue" | "filled_black";
  size?: "large" | "medium" | "small";
  text?: "signin_with" | "signup_with" | "continue_with" | "signin";
  shape?: "rectangular" | "pill" | "circle" | "square";
  width?: number;
  locale?: string;
}

export interface GoogleAccountsId {
  initialize: (configuracion: ConfiguracionGoogle) => void;
  renderButton: (contenedor: HTMLElement, opciones: OpcionesBotonGoogle) => void;
  cancel?: () => void;
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleAccountsId } };
  }
}

let carga: Promise<GoogleAccountsId> | null = null;

/** Carga el script una sola vez por página y resuelve con google.accounts.id. */
export function cargarGoogle(): Promise<GoogleAccountsId> {
  if (window.google?.accounts?.id) {
    return Promise.resolve(window.google.accounts.id);
  }

  if (!carga) {
    carga = new Promise<GoogleAccountsId>((resolver, rechazar) => {
      const fallar = () => {
        carga = null;
        rechazar(new Error("No se pudo cargar Google Identity Services."));
      };
      const listo = () => (window.google?.accounts?.id ? resolver(window.google.accounts.id) : fallar());

      const existente = document.querySelector<HTMLScriptElement>(`script[src="${URL_SCRIPT_GOOGLE}"]`);
      if (existente) {
        existente.addEventListener("load", listo, { once: true });
        existente.addEventListener("error", fallar, { once: true });
        return;
      }

      const script = document.createElement("script");
      script.src = URL_SCRIPT_GOOGLE;
      script.async = true;
      script.defer = true;
      script.addEventListener("load", listo, { once: true });
      script.addEventListener("error", fallar, { once: true });
      document.head.appendChild(script);
    });
  }

  return carga;
}

/** Solo para pruebas: olvida la carga anterior. */
export function reiniciarCargaGoogle(): void {
  carga = null;
}
