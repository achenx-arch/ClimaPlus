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

export interface CambiarPasswordRequest {
  passwordActual: string;
  passwordNueva: string;
}

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
