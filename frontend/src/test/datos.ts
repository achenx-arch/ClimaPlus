// Respuestas de ejemplo copiadas de los contratos reales (docs/api/contrato-*.md).
import type {
  Alerta,
  AlertaAdmin,
  ClimaActual,
  ConfiguracionAlerta,
  ConsultaHistorial,
  ConsultaHistorialDetalle,
  Favorito,
  Pagina,
  Pronostico,
  Ubicacion,
  Usuario,
} from "../types/api";

export const usuario: Usuario = {
  idUsuario: 12,
  nombre: "Ana López",
  correo: "ana.lopez@ejemplo.com",
  rol: "USUARIO",
  activo: true,
  fechaCreacion: "2026-10-25T19:54:00Z",
};

export const administrador: Usuario = { ...usuario, idUsuario: 1, nombre: "Admin ClimaPlus", correo: "admin@ejemplo.com", rol: "ADMINISTRADOR" };

export const coban: Ubicacion = {
  idUbicacion: 12,
  nombre: "Cobán",
  region: "Alta Verapaz",
  pais: "GT",
  latitud: 15.4708,
  longitud: -90.3708,
};

export const guatemala: Ubicacion = {
  idUbicacion: 7,
  nombre: "Ciudad de Guatemala",
  region: "Guatemala",
  pais: "GT",
  latitud: 14.6418,
  longitud: -90.5132,
};

export function climaActual(cambios: Partial<ClimaActual> = {}): ClimaActual {
  return {
    ubicacion: coban,
    consulta: { idConsulta: 345, fechaConsultaUtc: "2026-10-27T18:21:04Z", desdeCache: false },
    temperatura: 22.46,
    sensacionTermica: 22.1,
    humedad: 70,
    velocidadViento: 3.1,
    codigoCondicion: 802,
    descripcion: "nubes dispersas",
    detalle: {
      version: 1,
      observadoEnUtc: "2026-10-27T18:20:00Z",
      zonaHorariaSegundos: -21600,
      esDeDia: true,
      temperaturaMin: 16.0,
      temperaturaMax: 25.0,
      presionHpa: 1018,
      direccionVientoGrados: 45,
      amanecerUtc: "2026-10-27T11:58:00Z",
      atardecerUtc: "2026-10-27T23:41:00Z",
    },
    alertasGeneradas: [],
    ...cambios,
  };
}

export const pronostico: Pronostico = {
  ubicacion: coban,
  consulta: { idConsulta: 346, fechaConsultaUtc: "2026-10-27T18:25:00Z", desdeCache: false },
  zonaHorariaSegundos: -21600,
  dias: [
    {
      fecha: "2026-10-28",
      completo: true,
      temperaturaMax: 22.3,
      temperaturaMin: 15.1,
      codigoCondicion: 500,
      descripcion: "lluvia ligera",
      probabilidadLluvia: 80,
      bloques: [
        { fechaHoraUtc: "2026-10-28T06:00:00Z", temperatura: 15.6, humedad: 95, velocidadViento: 1.2, codigoCondicion: 804, descripcion: "nubes", probabilidadLluvia: 20, esDeDia: false },
        { fechaHoraUtc: "2026-10-28T18:00:00Z", temperatura: 21.9, humedad: 70, velocidadViento: 2.5, codigoCondicion: 500, descripcion: "lluvia ligera", probabilidadLluvia: 80, esDeDia: true },
      ],
    },
    {
      fecha: "2026-10-29",
      completo: false,
      temperaturaMax: 24.0,
      temperaturaMin: 16.0,
      codigoCondicion: 800,
      descripcion: "cielo claro",
      probabilidadLluvia: 10,
      bloques: [
        { fechaHoraUtc: "2026-10-29T18:00:00Z", temperatura: 24.0, humedad: 60, velocidadViento: 3.0, codigoCondicion: 800, descripcion: "cielo claro", probabilidadLluvia: 10, esDeDia: true },
      ],
    },
  ],
};

export function favorito(cambios: Partial<Favorito> = {}): Favorito {
  return {
    idFavorito: 7,
    fechaCreacion: "2026-10-27T20:15:42.123Z",
    ubicacion: coban,
    ultimaConsulta: {
      idConsulta: 345,
      fechaConsulta: "2026-10-27T18:21:04Z",
      temperatura: 22.46,
      codigoCondicion: 802,
      descripcion: "nubes dispersas",
    },
    ...cambios,
  };
}

export function pagina<T>(elementos: T[], cambios: Partial<Pagina<T>> = {}): Pagina<T> {
  return {
    elementos,
    pagina: 1,
    tamanoPagina: 20,
    totalElementos: elementos.length,
    totalPaginas: elementos.length === 0 ? 0 : 1,
    ...cambios,
  };
}

export const consultaPronostico: ConsultaHistorial = {
  idConsulta: 58,
  tipo: "PRONOSTICO",
  fechaConsultaUtc: "2026-10-28T19:05:11.402Z",
  ubicacion: coban,
  temperatura: null,
  humedad: null,
  velocidadViento: null,
  codigoCondicion: null,
  descripcion: null,
};

export const consultaActual: ConsultaHistorial = {
  idConsulta: 57,
  tipo: "ACTUAL",
  fechaConsultaUtc: "2026-10-28T19:04:50.118Z",
  ubicacion: coban,
  temperatura: 18.4,
  humedad: 88,
  velocidadViento: 2.1,
  codigoCondicion: 500,
  descripcion: "lluvia ligera",
};

export const detalleActual: ConsultaHistorialDetalle = {
  idConsulta: 57,
  tipo: "ACTUAL",
  fechaConsultaUtc: "2026-10-28T19:04:50.118Z",
  ubicacion: coban,
  actual: {
    temperatura: 18.4,
    sensacionTermica: 18.6,
    humedad: 88,
    velocidadViento: 2.1,
    codigoCondicion: 500,
    descripcion: "lluvia ligera",
    detalle: { ...climaActual().detalle },
  },
  pronostico: null,
};

export function regla(cambios: Partial<ConfiguracionAlerta> = {}): ConfiguracionAlerta {
  return {
    idConfiguracion: 5,
    tipo: "TEMPERATURA_ALTA",
    ubicacion: coban,
    valorUmbral: 32,
    activa: true,
    fechaCreacion: "2026-10-27T21:00:00Z",
    ...cambios,
  };
}

export function alerta(cambios: Partial<Alerta> = {}): Alerta {
  return {
    idAlerta: 91,
    tipo: "TEMPERATURA_ALTA",
    mensaje: "Cobán: 33.4 C supera tu umbral de 32 C",
    estado: "NUEVA",
    valorDetectado: 33.4,
    valorUmbral: 32,
    fechaGeneracion: "2026-10-28T18:21:04.512345Z",
    ubicacion: coban,
    origen: { tipo: "OPENWEATHER", idConsulta: 345, idLectura: null, idDispositivo: null, nombreDispositivo: null },
    ...cambios,
  };
}

export function alertaAdmin(cambios: Partial<AlertaAdmin> = {}): AlertaAdmin {
  return {
    ...alerta(),
    usuario: { idUsuario: 12, nombre: "Ana López", correo: "ana.lopez@ejemplo.com" },
    ...cambios,
  };
}
