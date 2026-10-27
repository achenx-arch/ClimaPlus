import type {
  LoginRequest,
  LoginResponse,
  RegistroRequest,
  Usuario,
} from "../types/auth";

// URL del backend. La configuraremos cuando confirmemos su puerto.
const API_URL = import.meta.env.VITE_API_URL || "http://localhost:5000";

async function procesarRespuesta<T>(
  respuesta: Response
): Promise<T> {
  if (!respuesta.ok) {
    let mensaje = "Ocurrió un error al comunicarse con el servidor.";

    try {
      const datos = await respuesta.json();

      mensaje =
        datos.detail ||
        datos.title ||
        datos.message ||
        mensaje;

      if (datos.errors) {
        const errores = Object.values(datos.errors).flat();
        if (errores.length > 0) {
          mensaje = errores.join(" ");
        }
      }
    } catch {
      // Mantener el mensaje general si no hay JSON.
    }

    throw new Error(mensaje);
  }

  return respuesta.json() as Promise<T>;
}

// POST /api/auth/login
export async function iniciarSesion(
  datos: LoginRequest
): Promise<LoginResponse> {
  const respuesta = await fetch(`${API_URL}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(datos),
  });

  return procesarRespuesta<LoginResponse>(respuesta);
}

// POST /api/auth/register
export async function registrarUsuario(
  datos: RegistroRequest
): Promise<Usuario> {
  const respuesta = await fetch(`${API_URL}/api/auth/register`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(datos),
  });

  return procesarRespuesta<Usuario>(respuesta);
}

// GET /api/auth/me
export async function obtenerUsuarioActual(
  token: string
): Promise<Usuario> {
  const respuesta = await fetch(`${API_URL}/api/auth/me`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  return procesarRespuesta<Usuario>(respuesta);
}