# ClimaPlus – Análisis OWASP Top 10 (2021) · Fase 8

**Fecha:** 2026-10-29 · **Rama:** `feature/fase8-seguridad` · **Alcance:** API ASP.NET Core 8, frontend React y API de dispositivos ESP32
**Referencias:** OWASP Top 10 2021, OWASP ASVS 4.0.3, OWASP Secure Headers Project, `docs/fase-1/07-seguridad-inicial.md`

## Resumen

Se revisaron los diez riesgos del OWASP Top 10 2021 contra el código del backend, el frontend y la API de dispositivos.
La mayoría de los controles ya existían desde las fases 2 a 7. En esta fase se corrigieron **6 hallazgos**, se
agregaron **19 pruebas automatizadas de seguridad en el backend y 1 en el frontend**, y se dejaron documentados los
riesgos residuales que dependen del despliegue (Fase 9).

| Resultado | Cantidad |
|---|---|
| Riesgos del Top 10 con controles implementados y probados | 10 de 10 |
| Hallazgos corregidos en la Fase 8 | 6 |
| Vulnerabilidades conocidas en dependencias | 0 (NuGet: 89 paquetes · npm: 0 con `npm audit`) |
| Pruebas totales | 683 (507 xUnit · 105 Vitest · 71 SQL), todas aprobadas |

## Hallazgos corregidos en esta fase

| # | Riesgo | Hallazgo | Corrección | Prueba |
|---|---|---|---|---|
| H-1 | A05 | La API solo enviaba `X-Content-Type-Options`. | Cabeceras CSP, X-Frame-Options, Referrer-Policy, Permissions-Policy, COOP y `Cache-Control: no-store`, también en respuestas de error. HSTS de 1 año fuera de desarrollo. | `Seguridad/CabecerasSeguridadTests` |
| H-2 | A05 | Quedaba el controlador de ejemplo `WeatherForecast` de la plantilla. | Eliminado (menos superficie de ataque). | `ControladorDePlantilla_WeatherForecast_YaNoExiste` |
| H-3 | A05 / A04 | Kestrel anunciaba su nombre (`Server: Kestrel`) y aceptaba cuerpos de hasta 30 MB. | Sin cabecera `Server`; cuerpo máximo de 1 MB (Kestrel y middleware → 413). | `CuerpoDemasiadoGrande_Responde413_SinProcesarlo` |
| H-4 | A04 | Las rutas con sesión (favoritos, alertas, dispositivos…) no tenían límite de peticiones. | Límite general de 300 peticiones por minuto por IP, además de los límites específicos. | `LimiteGeneralTests` |
| H-5 | A07 | El límite por IP no frenaba un ataque de contraseñas distribuido contra un mismo correo. | Bloqueo temporal por cuenta: 5 fallos → 15 minutos (429 + `Retry-After`). Igual para correos inexistentes. | `AutenticacionAvanzadaTests` (6 pruebas) |
| H-6 | A07 | Cambiar la contraseña no cerraba las sesiones abiertas (el JWT seguía valiendo hasta expirar). | Sello de sesión `sv` en el JWT (HMAC del hash de la contraseña). Al cambiarla, todos los tokens anteriores se rechazan; el frontend lleva al login con un aviso. | `CambiarContrasena_RevocaTodasLasSesionesAbiertas`, `TokenFirmadoConLaClaveCorrecta_PeroSinSello_SeRechaza`, prueba Vitest de M16 |

## Análisis por riesgo

### A01 – Broken Access Control · Cumple
- Todo endpoint es privado por defecto (`FallbackPolicy` con usuario autenticado); los públicos se marcan con `[AllowAnonymous]`.
- Rutas de administración con `[Authorize(Roles = "ADMINISTRADOR")]`; el frontend también protege rutas por rol (`RutaProtegida`).
- **Control de propiedad (IDOR):** favoritos, historial, alertas, reglas y dispositivos se filtran siempre por el `id_usuario` del token; un recurso ajeno responde **404**, igual que uno inexistente.
- El rol y el estado de la cuenta no se pueden cambiar desde el perfil (RN-BD-37, RN-BD-38).
- La API Key de un dispositivo no sirve como sesión de usuario, y el JWT no sirve para enviar lecturas.
- CORS limitado a los orígenes configurados (`http://localhost:5173` en desarrollo).
- **Pruebas:** `FavoritosTests`, `HistorialTests`, `AlertasTests`, `DispositivosTests.DispositivoAjeno_SeTrataComoInexistente_404`, `AutenticacionTests`, `navegacion.test.tsx`.

### A02 – Cryptographic Failures · Cumple
- Contraseñas con `PasswordHasher` de ASP.NET Core Identity (PBKDF2-HMAC-SHA512 con sal y rehash automático).
- JWT firmado con HS256 y una clave de al menos 32 bytes; la API valida firma, emisor, audiencia, algoritmo y expiración.
- API Key del ESP32: 32 bytes aleatorios (`RandomNumberGenerator`); solo se guarda su SHA-256 (RN-BD-28) y se muestra una sola vez.
- Secretos (cadena de conexión, clave JWT, API Key de OpenWeather) solo en User Secrets o variables de entorno; nunca en el repositorio ni en logs.
- `Cache-Control: no-store` en las respuestas de la API (tokens y datos personales). HTTPS obligatorio fuera de desarrollo (redirección + HSTS).
- **Pruebas:** `ApiKeyDispositivoTests`, `Registrar_Devuelve201_ConApiKeyUnaSolaVez_YGuardaSoloElHash`, `RespuestaExitosa_TieneCabecerasDeSeguridad_YNoSeGuardaEnCache`.

### A03 – Injection · Cumple
- Acceso a datos con EF Core LINQ (consultas parametrizadas). La única consulta SQL escrita a mano (`pg_advisory_xact_lock`) usa `ExecuteSqlAsync` interpolado, que también se parametriza. No hay `FromSqlRaw` ni `ExecuteSqlRaw`.
- Validación estricta de entradas: DataAnnotations, expresiones regulares (nombres sin `<` `>`), rangos numéricos y `JsonUnmappedMemberHandling.Disallow` (rechaza campos desconocidos).
- Frontend en React (escapa todo el texto). No hay `dangerouslySetInnerHTML`, `eval` ni `innerHTML`. CSP en el sitio compilado (`script-src 'self'`).
- Los parámetros enviados a OpenWeather se codifican con `Uri.EscapeDataString`.
- **Pruebas:** `SeguridadEntradasTests`, `Registrar_DatosInvalidos_Responde400` (incluye `<script>`), `LecturaInvalida_Responde400_YNoGuardaNada`, 71 pruebas SQL de integridad.

### A04 – Insecure Design · Cumple
- Rate limiting por capas: autenticación 5/min, clima 30/min, lecturas 30/min por dispositivo y **límite general 300/min por IP** (nuevo).
- **Bloqueo temporal por cuenta** tras 5 fallos (nuevo).
- Límite de 1 MB en el cuerpo de las peticiones (nuevo).
- Reglas de negocio en el servidor: anti-duplicado de alertas en transacción con bloqueo por usuario, transacciones atómicas para lecturas y consultas, CHECK y UNIQUE en PostgreSQL como segunda línea de defensa.
- Caché de clima en la base de datos para proteger la cuota de OpenWeather (D-23).
- **Pruebas:** `LimiteGeneralTests`, `ClimaRateLimitingTests`, `LecturasRateLimitingTests`, `MotorAlertasPostgreSqlTests.PeticionesSimultaneas_DelMismoUsuario_GeneranUnaSolaAlerta`.

### A05 – Security Misconfiguration · Cumple (con H-1, H-2 y H-3)
- Cabeceras de seguridad en todas las respuestas, incluidas 401, 404, 413 y 500:

  | Cabecera | Valor |
  |---|---|
  | Content-Security-Policy | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'` (Swagger tiene la suya) |
  | X-Frame-Options | `DENY` |
  | X-Content-Type-Options | `nosniff` |
  | Referrer-Policy | `no-referrer` |
  | Permissions-Policy | `camera=(), microphone=(), geolocation=(), payment=(), usb=()` |
  | Cross-Origin-Opener-Policy | `same-origin` |
  | Cache-Control | `no-store` |
  | Strict-Transport-Security | `max-age=31536000; includeSubDomains` (solo HTTPS y fuera de desarrollo) |

- Errores como ProblemDetails, sin mensajes internos ni pila de llamadas.
- Swagger solo en desarrollo (en Producción responde 401).
- Configuración validada al arrancar (`ValidateOnStart`): la API no inicia con una clave JWT débil o límites inválidos.
- Frontend compilado con CSP (`default-src 'self'`; conexiones solo a la API configurada) y `referrer` `no-referrer`. Verificado en Chromium: 0 violaciones de CSP.
- **Pruebas:** `CabecerasSeguridadTests` (9 pruebas).

### A06 – Vulnerable and Outdated Components · Cumple
- **NuGet:** los 89 paquetes resueltos (directos y transitivos) se cruzaron con los 1188 avisos de la GitHub Advisory Database para NuGet → **0 vulnerables**. Versiones al día dentro de .NET 8 LTS (EF Core 8.0.22, Npgsql 8.0.11, JwtBearer 8.0.22).
- **npm:** `npm audit` → **0 vulnerabilidades** (incluidas las dependencias de desarrollo).
- `package-lock.json` versionado para instalaciones reproducibles.
- Cómo repetirlo:

  ```bash
  dotnet list backend/ClimaPlus.sln package --vulnerable --include-transitive
  cd frontend && npm audit
  ```

### A07 – Identification and Authentication Failures · Cumple (con H-5 y H-6)
- Política de contraseñas: 8 a 128 caracteres, con letra y número. El hash nunca sale de la API.
- Mensaje genérico "Correo o contraseña incorrectos" y verificación de un hash ficticio cuando el correo no existe (no revela qué cuentas existen ni por el tiempo de respuesta).
- **Bloqueo temporal por cuenta** (5 fallos → 15 minutos) además del límite por IP.
- JWT de 60 minutos revalidado en cada petición contra la base de datos: se rechaza si la cuenta se desactivó, si cambió el rol o si **cambió la contraseña** (sello `sv`).
- El token no contiene datos personales (solo `sub`, `role`, `sv`, `jti` y fechas).
- Frontend: token en memoria (o en `sessionStorage` solo con "Recordarme", nunca en `localStorage`), cierre automático al expirar y ante cualquier 401.
- Cambio de contraseña: exige la contraseña actual, tiene límite de 5 intentos por minuto y cierra todas las sesiones.
- **Pruebas:** `AutenticacionTests`, `PerfilTests`, `AutenticacionAvanzadaTests` (9 pruebas), `auth.test.tsx`, `modulos.test.tsx`.

### A08 – Software and Data Integrity Failures · Cumple
- El JWT exige el algoritmo HS256 (`ValidAlgorithms`): no acepta `alg: none` ni otros algoritmos.
- El servidor asigna fechas, propietarios y orígenes (lecturas, alertas, consultas); el cliente no puede enviarlos (`Disallow`).
- Dependencias fijadas por `package-lock.json` y versiones exactas en los `.csproj`.
- Todo cambio entra a `main` por Pull Request revisado.
- **Pruebas:** `Token_firmado_con_otra_clave_devuelve_401` (en `AutenticacionTests`), `LecturaInvalida_Responde400_YNoGuardaNada` (rechaza `fechaLectura`).

### A09 – Security Logging and Monitoring Failures · Cumple
- Eventos de seguridad con la categoría `ClimaPlus.Seguridad`: `login_fallido`, `login_bloqueado`, `token_rechazado` (con motivo `sesion_revocada`, `cuenta_desactivada`, `rol_cambiado`), `rate_limit_excedido`, `dispositivo_rechazado`, `cambio_password_fallido`, `password_cambiada`.
- Nunca se registran contraseñas, tokens, API Keys ni la API Key de OpenWeather; los correos aparecen enmascarados (`a***@dominio.com`).
- **Pruebas:** `Rechazos_NoRegistranLaApiKeyEnLogs`, `ElBloqueo_SeRegistraEnLogs_SinLaContrasena`, `ApiKey_NoApareceEnListaNiDetalle_NiEnLogs`.

### A10 – Server-Side Request Forgery · Cumple
- La API solo hace peticiones salientes a OpenWeather. La URL base viene de la configuración, no del usuario; el usuario solo aporta texto de búsqueda o el id de una ubicación ya guardada (D-22), y los parámetros se codifican.
- El ESP32 no recibe URLs de la API ni la API llama al ESP32.
- En las pruebas, OpenWeather apunta a un dominio `.invalid` y se simula: ninguna prueba sale a internet.

## Riesgos residuales y recomendaciones

| # | Riesgo | Mitigación actual | Recomendación |
|---|---|---|---|
| R-1 | Con "Recordarme", el JWT queda en `sessionStorage` y un XSS podría leerlo. | React escapa el contenido, CSP `script-src 'self'`, token de 60 minutos. | Si se requiere más, migrar a cookie `HttpOnly` + `SameSite=Strict` (cambio de arquitectura D-01). |
| R-2 | No hay cierre de sesión en el servidor: un token robado vale hasta expirar. | Expira en 60 min; se revoca al cambiar la contraseña, desactivar la cuenta o cambiar el rol. | Lista de revocación si el alcance crece. |
| R-3 | Rate limiting y bloqueo por cuenta en memoria (una sola instancia). | Suficiente para el despliegue actual. | Con varias instancias, usar Redis u otro almacén compartido. |
| R-4 | No se comprueba si la contraseña aparece en filtraciones conocidas, ni hay MFA. | Política de longitud y complejidad, bloqueo por cuenta. | Integrar la API de Have I Been Pwned (k-anonymity) y MFA (TOTP) en una versión futura. |
| R-5 | El ESP32 envía la API Key por HTTP dentro de la red local. | Red WiFi WPA2, clave revocable desde M11, límite por dispositivo. | En el despliegue (Fase 9), exponer la API con HTTPS también para el ESP32. |
| R-6 | `frame-ancestors` y HSTS del frontend no se pueden poner en una etiqueta `<meta>`. | La API sí los envía. | Configurarlos como cabeceras en el servidor web del frontend (Nginx, Fase 9). |
| R-7 | El escaneo de dependencias es manual. | 0 vulnerabilidades a la fecha. | Automatizarlo en GitHub Actions y activar Dependabot (Fase 9). |
| R-8 | `AllowedHosts` es `*`. | Detrás de un proxy en el despliegue. | Fijar el dominio real en la configuración de Producción (Fase 9). |
