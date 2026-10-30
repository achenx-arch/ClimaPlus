import "./Layouts.css";

/** Aviso de un módulo sin endpoints en el backend: no se simulan operaciones. */
export default function ModuloPendiente({ titulo, descripcion, requisitos }: { titulo: string; descripcion: string; requisitos: string }) {
  return (
    <section className="cp-aviso" aria-labelledby="modulo-pendiente-titulo">
      <div className="cp-aviso-icono">
        <i className="bi bi-hourglass-split" aria-hidden="true"></i>
      </div>
      <h1 id="modulo-pendiente-titulo">{titulo}</h1>
      <p>{descripcion}</p>
      <p className="small mb-0">
        Módulo pendiente de implementar en el backend ({requisitos}). Esta pantalla no muestra
        ni modifica datos.
      </p>
    </section>
  );
}
