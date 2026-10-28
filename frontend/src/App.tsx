import { Routes, Route, useLocation } from 'react-router-dom'
import Navbar from './components/Navbar'
import Home from './pages/Home'
import Login from './pages/Login'
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

function App() {
  const location = useLocation()

  // Ocultar Navbar en las pantallas de autenticación
  const rutasSinNavbar = [
    '/login',
    '/registro',
    '/recuperar-contrasena'
  ]

const esRutaAdministrativa =
  location.pathname === '/admin' ||
  location.pathname.startsWith('/admin/')

const mostrarNavbar =
  !rutasSinNavbar.includes(location.pathname) &&
  !esRutaAdministrativa

  return (
    <>
      {mostrarNavbar && <Navbar />}

      <Routes>
        {/* M01 - Página principal */}
        <Route path="/" element={<Home />} />

        {/* M02 - Inicio de sesión */}
        <Route path="/login" element={<Login />} />

        {/* M03 - Registro de usuarios */}
        <Route path="/registro" element={<Register />} /> 
        
        {/* M04 - Clima actual */}
        <Route path="/clima" element={<CurrentWeather />} />
      
        {/* M05 - Pronóstico de 5 días */}
        <Route path="/pronostico" element={<Forecast />} />
      
        {/* M06 - Dashboard del usuario */}
        <Route path="/dashboard" element={<Dashboard />} />

        {/* M07 - Favoritos del usuario */}
        <Route path="/favoritos" element={<Favorites />} />

        {/* M08 - Historial de consultas */}
        <Route path="/historial" element={<History />} />

        {/* M09 - Alertas meteorológicas */}
        <Route path="/alertas" element={<Alerts />} />
        <Route path="/dispositivos" element={<Devices />} />
        <Route path="/dispositivos/:id" element={<DeviceDetails />} />
        <Route path="/admin" element={<AdminDashboard />} />
        <Route path="/admin/usuarios" element={<AdminUsers />} />
        <Route path="/admin/alertas" element={<AdminAlerts />} />

        {/* M10 - Configuración de alertas */}
        <Route path="/configuracion-alertas" element={<AlertSettings />} />

        {/* M16 - Perfil del usuario */}
        <Route path="/perfil" element={<UserProfile />} />

        {/* M17 - Perfil del administrador */}
        <Route path="/admin/perfil" element={<UserProfile />} />
      </Routes>
    </>
  )
}

export default App