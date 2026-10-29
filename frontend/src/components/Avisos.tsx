import { useNotificaciones } from "../context/notificaciones";

const ESTILOS = {
  alerta: { clase: "text-bg-warning", icono: "bi-bell-fill" },
  exito: { clase: "text-bg-success", icono: "bi-check-circle-fill" },
  error: { clase: "text-bg-danger", icono: "bi-exclamation-triangle-fill" },
  info: { clase: "text-bg-primary", icono: "bi-info-circle-fill" },
} as const;

/** Toasts de Bootstrap abajo a la derecha (08-mockups.md, "Mensajes de error y retroalimentación"). */
export default function Avisos() {
  const { avisos, cerrarAviso } = useNotificaciones();

  if (avisos.length === 0) return null;

  return (
    <div className="toast-container position-fixed bottom-0 end-0 p-3" style={{ zIndex: 1080 }}>
      {avisos.map((aviso) => {
        const estilo = ESTILOS[aviso.tipo];
        return (
          <div
            key={aviso.id}
            className={`toast show align-items-center border-0 ${estilo.clase}`}
            role={aviso.tipo === "error" ? "alert" : "status"}
            aria-live={aviso.tipo === "error" ? "assertive" : "polite"}
            aria-atomic="true"
          >
            <div className="d-flex">
              <div className="toast-body">
                <i className={`bi ${estilo.icono} me-2`} aria-hidden="true"></i>
                {aviso.texto}
              </div>
              <button
                type="button"
                className="btn-close me-2 m-auto"
                aria-label="Cerrar aviso"
                onClick={() => cerrarAviso(aviso.id)}
              ></button>
            </div>
          </div>
        );
      })}
    </div>
  );
}
