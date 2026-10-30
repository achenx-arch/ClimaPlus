import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'

import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'
import 'bootstrap/dist/js/bootstrap.bundle.min.js'

import App from './App'
import { AuthProvider } from './context/AuthProvider'
import { NotificacionesProvider } from './context/NotificacionesProvider'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <NotificacionesProvider>
          <App />
        </NotificacionesProvider>
      </AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
)
