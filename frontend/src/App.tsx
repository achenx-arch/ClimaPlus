import { Routes, Route, useLocation } from 'react-router-dom'
import Navbar from './components/Navbar'
import Avisos from './components/Avisos'
import RutaProtegida from './components/RutaProtegida'
import { LayoutAdmin, LayoutSegunSesion, LayoutUsuario } from './components/MenuLateral'
import { useAuth } from './context/auth'
import Home from './pages/Home'
import Login from './pages/Login'
import RecoverPassword from './pages/RecoverPassword'
import Register from './pages/Register'
import Forecast from './pages/Forecast'
import CurrentWeather from './pages/CurrentWeather'
import Dashboard from './pages/Dashboard'
import Favorites from './pages/Favorites'
import Alerts from './pages/Alerts'
import History from './pages/History'
import AlertSettings from './pages/AlertSettings'
import Devices from "./pages/Devices";
import DeviceDetails from "./pages/DeviceDetails";
import AdminDashboard from "./pages/AdminDashboard";
import AdminUsers from "./pages/AdminUsers";
import AdminAlerts from "./pages/AdminAlerts";
import UserProfile from "./pages/UserProfile";
import NoEncontrada from "./pages/NoEncontrada";

// Rutas (corrección de navegación, 2026-10-29):
// - Públicas: /, /login, /registro, /recuperar-contrasena, /clima, /pronostico.
//   Clima y pronóstico muestran el menú de "Mi espacio" cuando hay sesión.
// - Privadas (USUARIO y ADMINISTRADOR en su propia cuenta): layout con menú lateral persistente.
// - Administración: solo ADMINISTRADOR, con su propio menú lateral.
function App() {
  const location = useLocation()
  const { esAdministrador } = useAuth()

  // Pantallas de autenticación sin navbar (tienen su propio diseño M02/M03).
  const rutasSinNavbar = [
    '/login',
    '/registro',
    '/recuperar-contrasena'
  ]

  const esRutaAdministrativa =
    location.pathname === '/admin' ||
    location.pathname.startsWith('/admin/')

  // El administrador usa su menú lateral; una cuenta sin permiso ve la navbar normal junto a "Acceso denegado".
  const mostrarNavbar =
    !rutasSinNavbar.includes(location.pathname) &&
    !(esRutaAdministrativa && esAdministrador)

  return (
    <>
      {mostrarNavbar && <Navbar />}

      <Routes>
        {/* M01 - Página principal (pública, también con sesión) */}
        <Route path="/" element={<Home />} />

        {/* M02, M03 y recuperación (sin endpoint en el backend) */}
        <Route path="/login" element={<Login />} />
        <Route path="/registro" element={<Register />} />
        <Route path="/recuperar-contrasena" element={<RecoverPassword />} />

        {/* M04 y M05 - Públicas; con sesión, dentro del menú de "Mi espacio" */}
        <Route element={<LayoutSegunSesion />}>
          <Route path="/clima" element={<CurrentWeather />} />
          <Route path="/pronostico" element={<Forecast />} />
        </Route>

        {/* M06 a M12 y M16 - Con sesión */}
        <Route element={<RutaProtegida><LayoutUsuario /></RutaProtegida>}>
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/favoritos" element={<Favorites />} />
          <Route path="/historial" element={<History />} />
          <Route path="/alertas" element={<Alerts />} />
          <Route path="/configuracion-alertas" element={<AlertSettings />} />
          <Route path="/dispositivos" element={<Devices />} />
          <Route path="/dispositivos/:id" element={<DeviceDetails />} />
          <Route path="/perfil" element={<UserProfile />} />
        </Route>

        {/* M13 a M15 y M17 - Solo ADMINISTRADOR */}
        <Route element={<RutaProtegida rol="ADMINISTRADOR"><LayoutAdmin /></RutaProtegida>}>
          <Route path="/admin" element={<AdminDashboard />} />
          <Route path="/admin/alertas" element={<AdminAlerts />} />
          <Route path="/admin/usuarios" element={<AdminUsers />} />
          <Route path="/admin/perfil" element={<UserProfile />} />
        </Route>

        <Route path="*" element={<NoEncontrada />} />
      </Routes>

      <Avisos />
    </>
  )
}

export default App
