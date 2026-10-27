// Datos públicos del usuario
export interface Usuario {
  idUsuario: number;
  nombre: string;
  correo: string;
  rol: string;
  activo: boolean;
  fechaCreacion: string;
}

// Datos enviados al iniciar sesión
export interface LoginRequest {
  correo: string;
  password: string;
}

// Datos enviados al registrar una cuenta
export interface RegistroRequest {
  nombre: string;
  correo: string;
  password: string;
}

// Respuesta del backend al iniciar sesión
export interface LoginResponse {
  token: string;
  tipoToken: string;
  expiraEn: string;
  usuario: Usuario;
}