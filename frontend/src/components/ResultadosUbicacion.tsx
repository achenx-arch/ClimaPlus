import type { Ubicacion } from "../types/api";
import { lugar } from "../utils/clima";

/** Lista de coincidencias de GET /api/clima/ubicaciones para elegir una. */
export default function ResultadosUbicacion({
  resultados,
  alElegir,
  textoAccion = "Ver clima",
}: {
  resultados: Ubicacion[];
  alElegir: (ubicacion: Ubicacion) => void;
  textoAccion?: string;
}) {
  if (resultados.length === 0) return null;

  return (
    <div className="list-group my-3 text-start" aria-label="Resultados de la búsqueda">
      {resultados.map((ubicacion) => (
        <button
          key={ubicacion.idUbicacion}
          type="button"
          className="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
          onClick={() => alElegir(ubicacion)}
        >
          <span>
            <i className="bi bi-geo-alt me-2" aria-hidden="true"></i>
            <strong>{ubicacion.nombre}</strong>
            <span className="text-secondary ms-2">{lugar(ubicacion)}</span>
          </span>
          <span className="small text-primary">{textoAccion}</span>
        </button>
      ))}
    </div>
  );
}
