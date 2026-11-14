// Tipos de los contratos HTTP reales del backend (docs/api/contrato-*.md).
// Las propiedades usan exactamente los nombres JSON (camelCase) de ASP.NET Core.

export type Rol = "USUARIO" | "ADMINISTRADOR";
export type TipoConsulta = "ACTUAL" | "PRONOSTICO";
export type TipoAlerta = "LLUVIA" | "TEMPERATURA_ALTA" | "TEMPERATURA_BAJA";
export type EstadoAlerta = "NUEVA" | "LEIDA";
export type OrigenAlerta = "OPENWEATHER" | "ESP32";

/** Página de resultados (20 elementos por página). */
export interface Pagina<T> {
  elementos: T[];
  pagina: number;
  tamanoPagina: number;
  totalElementos: number;
  totalPaginas: number;
}

/** UbicacionResponse (contrato-clima-pronostico.md). */
export interface Ubicacion {
  idUbicacion: number;
  nombre: string;
  region: string | null;
  pais: string;
  latitud: number;
  longitud: number;
}

/** ConsultaRegistradaResponse. */
export interface ConsultaRegistrada {
  idConsulta: number;
  fechaConsultaUtc: string;
  desdeCache: boolean;
}

/** detalle de una consulta ACTUAL (versión 1). */
export interface DetalleClimaActual {
  version: number;
  observadoEnUtc: string;
  zonaHorariaSegundos: number;
  esDeDia: boolean;
  temperaturaMin: number | null;
  temperaturaMax: number | null;
  presionHpa: number | null;
  direccionVientoGrados: number | null;
  amanecerUtc: string | null;
  atardecerUtc: string | null;
}

/** AlertaGeneradaResponse (Fase 2H). */
export interface AlertaGenerada {
  idAlerta: number;
  tipo: TipoAlerta;
  mensaje: string;
  fechaGeneracion: string;
}

/** GET /api/clima/actual. Viento en m/s. */
export interface ClimaActual {
  ubicacion: Ubicacion;
  consulta: ConsultaRegistrada;
  temperatura: number;
  sensacionTermica: number;
  humedad: number;
  velocidadViento: number;
  codigoCondicion: number;
  descripcion: string;
  detalle: DetalleClimaActual;
  alertasGeneradas: AlertaGenerada[];
}

export interface BloquePronostico {
  fechaHoraUtc: string;
  temperatura: number;
  humedad: number;
  velocidadViento: number;
  codigoCondicion: number;
  descripcion: string;
  probabilidadLluvia: number;
  esDeDia: boolean;
}

export interface DiaPronostico {
  /** Fecha local de la ubicación, "AAAA-MM-DD". */
  fecha: string;
  completo: boolean;
  temperaturaMax: number;
  temperaturaMin: number;
  codigoCondicion: number;
  descripcion: string;
  probabilidadLluvia: number;
  bloques: BloquePronostico[];
}

/** GET /api/clima/pronostico. */
export interface Pronostico {
  ubicacion: Ubicacion;
  consulta: ConsultaRegistrada;
  zonaHorariaSegundos: number;
  dias: DiaPronostico[];
}

/** Usuario (contrato-auth-perfil.md). */
export interface Usuario {
  idUsuario: number;
  nombre: string;
  correo: string;
  rol: Rol;
  activo: boolean;
  fechaCreacion: string;
  /** Confirmó su correo con un código (autenticación segura). */
  correoVerificado: boolean;
  /** Tiene contraseña local. false en cuentas creadas con Google. */
  tienePassword: boolean;
  /** Tiene una cuenta de Google vinculada. */
  tieneGoogle: boolean;
}

export interface LoginRequest {
  correo: string;
  password: string;
}

export interface RegistroRequest {
  nombre: string;
  correo: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  tipoToken: string;
  expiraEn: string;
  usuario: Usuario;
}

export interface ActualizarPerfilRequest {
  nombre: string;
  correo: string;
}

/** POST /api/usuarios/me/password/cambio (paso 1): envía un código CAMBIAR_PASSWORD; todavía no cambia nada. */
export interface CambiarPasswordRequest {
  passwordActual: string;
  passwordNueva: string;
  confirmacion: string;
}

/** POST /api/usuarios/me/password/cambio/verificar (paso 2): el código más los mismos datos del paso 1. */
export interface VerificarCambioPasswordRequest extends CambiarPasswordRequest {
  desafio: string;
  codigo: string;
}

// ─── Autenticación segura (contrato-auth-perfil.md, secciones 4.1 a 4.9) ─────

export type PropositoDesafio =
  | "VERIFICAR_CORREO"
  | "INICIO_SESION"
  | "VINCULAR_GOOGLE"
  | "RECUPERAR_PASSWORD"
  | "CAMBIAR_PASSWORD";

/** RespuestaDesafio: 202 de registro, login (paso 1), recuperación, establecer y vincular Google. */
export interface RespuestaDesafio {
  desafio: string;
  proposito: PropositoDesafio;
  destino: string;
  expiraEn: string;
  reenviarEn: number;
}

/** RespuestaReenvio: 202 de POST /api/auth/reenviar-codigo (no repite el destino). */
export interface RespuestaReenvio {
  desafio: string;
  proposito: PropositoDesafio;
  expiraEn: string;
  reenviarEn: number;
}

/** VerificarCodigoRequest: verificar-correo, login/verificar, password/verificar y google/vincular/verificar. */
export interface VerificarCodigoRequest {
  desafio: string;
  codigo: string;
}

/** RespuestaPermiso: 200 de POST /api/auth/password/verificar. */
export interface RespuestaPermiso {
  permiso: string;
  expiraEn: string;
}

/** RestablecerPasswordRequest: POST /api/auth/password/restablecer. */
export interface RestablecerPasswordRequest {
  permiso: string;
  passwordNueva: string;
  confirmacion: string;
}

/** RespuestaInicioGoogle: 200 de GET /api/auth/google/iniciar. */
export interface RespuestaInicioGoogle {
  clientId: string;
  nonce: string;
  expiraEn: string;
}

export type UsoGoogle = "login" | "vincular";

/** Favorito (contrato-favoritos.md). */
export interface UltimaConsulta {
  idConsulta: number;
  fechaConsulta: string;
  temperatura: number | null;
  codigoCondicion: number | null;
  descripcion: string | null;
}

export interface Favorito {
  idFavorito: number;
  fechaCreacion: string;
  ubicacion: Ubicacion;
  ultimaConsulta: UltimaConsulta | null;
}

/** Historial (contrato-historial.md). */
export interface ConsultaHistorial {
  idConsulta: number;
  tipo: TipoConsulta;
  fechaConsultaUtc: string;
  ubicacion: Ubicacion;
  temperatura: number | null;
  humedad: number | null;
  velocidadViento: number | null;
  codigoCondicion: number | null;
  descripcion: string | null;
}

export interface ConsultaHistorialDetalle {
  idConsulta: number;
  tipo: TipoConsulta;
  fechaConsultaUtc: string;
  ubicacion: Ubicacion;
  actual: {
    temperatura: number;
    sensacionTermica: number;
    humedad: number;
    velocidadViento: number;
    codigoCondicion: number;
    descripcion: string;
    detalle: DetalleClimaActual | null;
  } | null;
  pronostico: {
    zonaHorariaSegundos: number | null;
    dias: DiaPronostico[];
  } | null;
}

export interface FiltroHistorial {
  tipo?: TipoConsulta;
  idUbicacion?: number;
  desde?: string;
  hasta?: string;
  pagina?: number;
}

/** Configuración de alerta (contrato-configuracion-alertas.md). */
export interface ConfiguracionAlerta {
  idConfiguracion: number;
  tipo: TipoAlerta;
  ubicacion: Ubicacion | null;
  valorUmbral: number | null;
  activa: boolean;
  fechaCreacion: string;
}

export interface DatosConfiguracionAlerta {
  tipo: TipoAlerta;
  idUbicacion: number | null;
  valorUmbral: number | null;
  activa: boolean;
}

/** Alertas generadas (contrato-alertas-generadas.md). */
export interface OrigenAlertaInfo {
  tipo: OrigenAlerta;
  idConsulta: number | null;
  idLectura: number | null;
  idDispositivo: number | null;
  nombreDispositivo: string | null;
}

export interface Alerta {
  idAlerta: number;
  tipo: TipoAlerta;
  mensaje: string;
  estado: EstadoAlerta;
  valorDetectado: number | null;
  valorUmbral: number | null;
  fechaGeneracion: string;
  ubicacion: Ubicacion;
  origen: OrigenAlertaInfo;
}

export interface AlertaAdmin extends Alerta {
  usuario: { idUsuario: number; nombre: string; correo: string };
}

export interface FiltroAlertas {
  estado?: EstadoAlerta;
  tipo?: TipoAlerta;
  pagina?: number;
}

export interface FiltroAlertasAdmin extends FiltroAlertas {
  origen?: OrigenAlerta;
  desde?: string;
  hasta?: string;
  idUsuario?: number;
}

// ─── Dispositivos ESP32 (contrato-dispositivos-lecturas.md) ──────────────────

/** Última lectura resumida dentro del objeto Dispositivo. */
export interface UltimaLecturaDispositivo {
  idLectura: number;
  fechaLectura: string;
  temperatura: number | null;
  humedad: number | null;
  lluvia: boolean | null;
  luz: number | null;
}

/** Objeto Dispositivo tal como lo devuelve GET /api/dispositivos y GET /api/dispositivos/{id}. */
export interface Dispositivo {
  idDispositivo: number;
  nombre: string;
  activo: boolean;
  /** true si la última lectura llegó hace 5 minutos o menos (DP-F7-1). */
  enLinea: boolean;
  /** null si el dispositivo nunca envió datos. */
  ultimaConexion: string | null;
  fechaCreacion: string;
  ubicacion: Ubicacion;
  ultimaLectura: UltimaLecturaDispositivo | null;
}

/** Una lectura individual paginada de GET /api/dispositivos/{id}/lecturas. */
export interface LecturaDispositivo {
  idLectura: number;
  fechaLectura: string;
  temperatura: number | null;
  humedad: number | null;
  lluvia: boolean | null;
  luz: number | null;
}

/** Respuesta de POST /api/dispositivos (201). La apiKey se muestra una única vez (RN-BD-28). */
export interface DispositivoRegistrado {
  dispositivo: Dispositivo;
  apiKey: string;
  aviso: string;
}
