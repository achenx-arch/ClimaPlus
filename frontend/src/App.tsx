import { Routes, Route, useLocation } from 'react-router-dom'
import Navbar from './components/Navbar'
import Home from './pages/Home'
import Login from './pages/Login'
import Register from './pages/Register'

function App() {
  const location = useLocation()

  // Ocultar Navbar en las pantallas de autenticación
  const rutasSinNavbar = [
    '/login',
    '/registro',
    '/recuperar-contrasena'
  ]

  const mostrarNavbar = !rutasSinNavbar.includes(location.pathname)

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
      </Routes>
    </>
  )
}

export default App