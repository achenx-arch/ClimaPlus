import "./AdminUsers.css";
import EnlaceVolver from "../components/EnlaceVolver";
import ModuloPendiente from "../components/ModuloPendiente";

// M14 – Gestión de usuarios. El backend todavía no tiene GET /api/admin/usuarios ni los PATCH de
// rol y estado (RF-26 a RF-28), así que no se muestran usuarios ni se simulan operaciones.
export default function AdminUsers() {
  return (
    <div className="admin-users-layout">
    <main className="admin-users-page">
      <div className="admin-users-container">
        <EnlaceVolver to="/admin">Volver al panel administrativo</EnlaceVolver>

        <span className="admin-users-label d-block">ADMINISTRACIÓN</span>

        <div className="admin-users-header">
          <h1>Gestión de usuarios</h1>
          <p>Consulta y administra los usuarios registrados en ClimaPlus.</p>
        </div>

        <ModuloPendiente
          titulo="Módulo pendiente"
          descripcion="La lista de usuarios, el cambio de rol y la activación o desactivación de cuentas estarán disponibles cuando el backend publique sus endpoints."
          requisitos="RF-26, RF-27 y RF-28"
        />
      </div>
    </main>
    </div>
  );
}
