# ClimaPlus – Notas de la versión: autenticación segura (migración 004)

Diseño aprobado el 9 de noviembre de 2026. Rama: `feature/auth-correo-google-recuperacion`.

## Qué cambia para los usuarios

- **Todas las sesiones abiertas se cierran una vez** al desplegar esta versión. El sello de sesión (`sv`) del JWT
  ahora sale de la columna nueva `usuario.sello_seguridad` en lugar del hash de la contraseña (que no existe en las
  cuentas creadas con Google). Basta con volver a iniciar sesión.
- Las cuentas existentes quedan con `correo_verificado = false`: no hay evidencia de que su correo se haya
  verificado. En su primer inicio de sesión recibirán un código de verificación por correo.
- **Inicio de sesión en dos pasos:** después de la contraseña, la API envía un código de 6 dígitos al correo
  (válido 5 minutos) y solo entrega la sesión al validarlo (`POST /api/auth/login` responde 202 y
  `POST /api/auth/login/verificar` responde 200 con el JWT). El servidor de correo debe estar configurado antes de
  desplegar: sin él nadie podrá entrar.
- **Bloqueo temporal:** 5 fallos (contraseña o código) en 15 minutos bloquean esa cuenta desde esa IP; 20 fallos
  desde cualquier IP bloquean la cuenta en todas. Requiere `ProxyInverso` bien configurado detrás de un proxy: sin
  él, todos los usuarios comparten la IP del proxy.
- **Recuperación de contraseña real** ("¿Olvidaste tu contraseña?"): `POST /api/auth/password/solicitar`
  (202 siempre, exista o no la cuenta), `/password/verificar` (código de 6 dígitos → permiso de 10 minutos) y
  `/password/restablecer` (204). Restablecer cierra todas las sesiones, deja el correo verificado y envía un aviso.
  Una cuenta recibe como máximo 3 códigos de recuperación por hora; la IP, 5 solicitudes cada 15 minutos.
- **Cambiar contraseña desde el perfil** ahora también envía el aviso por correo y cierra todas las sesiones,
  incluida la actual (el usuario vuelve a iniciar sesión). **Desde la migración 005 (13-nov-2026)** exige además un
  código por correo: `POST /api/usuarios/me/password/cambio` (contraseña actual + nueva + confirmación → código
  `CAMBIAR_PASSWORD` de 5 minutos) y `POST /api/usuarios/me/password/cambio/verificar` (código → cambio). La
  contraseña no cambia hasta validar el código; el cambio anula los demás desafíos pendientes. El antiguo
  `PUT /api/usuarios/me/password` se eliminó. Ver [`migracion-005.md`](migracion-005.md).
- **Cuentas creadas con Google** (fase 5) podrán crear una contraseña local con
  `POST /api/usuarios/me/password/establecer`: exige la sesión y además un código enviado al correo.
- `GET /api/auth/me`, `GET /api/usuarios/me` y la respuesta del login agregan `correoVerificado`, `tienePassword`
  y `tieneGoogle` (no se quitó ningún campo).
- **"Continuar con Google"** (fase 5, opcional: se activa con `GOOGLE_CLIENT_ID`). Google Identity Services en el
  navegador y validación del ID token en la API (firma, emisor, audiencia, vencimiento, nonce de un solo uso).
  Entrar con Google entrega la sesión directamente, **sin código por correo**: Google es la prueba de identidad
  (decisión del 10-nov-2026, que reemplaza el "código obligatorio tras Google" del diseño del 9-nov). Las cuentas
  creadas con Google son `USUARIO` y no tienen contraseña local.
- **Nunca se vincula por correo:** si el Gmail ya tiene cuenta local, `POST /api/auth/google` responde 409. Vincular
  exige sesión, contraseña actual, un nonce de vinculación y el código por correo; desvincular exige la contraseña
  actual y que la cuenta tenga contraseña local. Ambas operaciones cierran las demás sesiones.
- Sin migración nueva: se usan `identidad_externa` (índices únicos por `sub` y por usuario) y
  `desafio_autenticacion.sujeto_externo` de la migración 004.
- Contraseñas, roles, favoritos, historial, reglas, dispositivos, lecturas y alertas no cambian.
- El ESP32 no se ve afectado: sigue autenticándose con `X-Device-Key`.

## Qué hace la migración 004

| Objeto | Cambio |
| --- | --- |
| `usuario.password_hash` | Admite `NULL` (cuentas solo de Google). Los hash existentes no se tocan. |
| `usuario.correo_verificado` | Nueva, `boolean NOT NULL DEFAULT false`. |
| `usuario.sello_seguridad` | Nueva, `uuid NOT NULL DEFAULT gen_random_uuid()`: un valor distinto por fila. |
| `desafio_autenticacion` | Tabla nueva: códigos de 6 dígitos (solo su HMAC), 9 CHECK, índice único parcial de un desafío vivo por usuario y propósito. |
| `identidad_externa` | Tabla nueva: vínculo con Google por su `sub`, 3 CHECK y 2 índices únicos. |

No contiene `DROP TABLE`, `DROP COLUMN`, `DELETE`, `UPDATE` ni `TRUNCATE` (lo comprueba la prueba
`ModeloAutenticacionTests.Script004_ConservaLosDatos_NoBorraNiActualizaFilas`).

## Cómo se probó

1. **Base con datos** (`climaplus_test`): migraciones 001 a 003, datos de muestra de las 8 tablas
   (`database/scripts/M004_datos_muestra_test.sql`), verificación previa, migración 004 y verificación posterior.
   Las filas por tabla, la huella MD5 de `id, correo, password_hash, rol, activo` y los usuarios por rol coinciden;
   los 3 usuarios quedaron sin verificar y con 3 sellos distintos.
2. **Base nueva**: migraciones 001 a 004 en orden, más las pruebas SQL 01 a 09 (95 PASS, 0 FAIL) y E-02/E-03.

## Cómo aplicarla (solo cuando lo decidas, siempre con respaldo)

Procedimiento completo y ensayado para la base oficial (diagnóstico, respaldo, restauración de prueba, migración,
verificación y recuperación): [`migracion-004-climaplus_db.md`](migracion-004-climaplus_db.md). Resumen:

La API de esta rama **necesita** la migración 004: sin ella, el inicio de sesión falla porque faltan columnas.

```powershell
# 1. Respaldo (PowerShell; ajusta usuario y base)
pg_dump -U postgres -d climaplus_db -Fc -f respaldo-antes-004.dump

# 2. Verificación previa (solo lectura) y guarda la salida
psql -U postgres -d climaplus_db -f database/scripts/M004_verificacion_previa.sql > previa.txt

# 3. Migración
psql -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -f database/migrations/004_AutenticacionSegura.sql

# 4. Verificación posterior: los puntos 3, 4 y 5 deben ser iguales a previa.txt
psql -U postgres -d climaplus_db -f database/scripts/M004_verificacion_posterior.sql > posterior.txt
```

**Docker con un volumen que ya existía** (los scripts de `database/migrations` solo corren con el volumen vacío):

```powershell
# El respaldo se genera dentro del contenedor y se copia: con ">" en PowerShell un respaldo binario se corrompe.
docker compose exec -T db pg_dump -U climaplus -d climaplus_db -Fc -f /tmp/respaldo-antes-004.dump
docker compose cp db:/tmp/respaldo-antes-004.dump .\respaldo-docker-antes-004.dump
docker compose cp database/migrations/004_AutenticacionSegura.sql db:/tmp/004.sql
docker compose exec -T db psql -U climaplus -d climaplus_db -v ON_ERROR_STOP=1 -f /tmp/004.sql
```

## Reversión

El `Down` de EF Core elimina las tablas y columnas nuevas, pero **no puede volver a poner `password_hash NOT NULL`
si ya existen cuentas solo de Google**: falla a propósito, sin rellenar contraseñas vacías. Para revertir con
cuentas de Google, restaura el respaldo previo (`pg_restore --clean`).

## Configuración nueva

| Variable (`.env`) / clave (User Secrets) | Uso |
| --- | --- |
| `CODIGOS_CLAVE_HMAC` / `Codigos:ClaveHmac` | **Obligatoria.** Clave HMAC de los códigos de 6 dígitos: 32 caracteres o más, aleatoria y distinta de `JWT_CLAVE`. Sin ella la API no arranca. |
| `Seguridad:IntentosLogin:MaxFallidos` / `MaxFallidosCuenta` / `MinutosBloqueo` | Bloqueo del login: 5 por cuenta + IP, 20 por cuenta, 15 minutos (valores por defecto en `appsettings.json`). |
| `Codigos:MaxFallosPorHora` | Códigos incorrectos por cuenta y hora en la verificación de correo y en la recuperación (10, por propósito). |
| `Codigos:MaxRecuperacionesPorHora` / `MinutosPermisoRestablecer` | Códigos de recuperación por cuenta y hora (3) y vigencia del permiso de restablecer (10 minutos). |
| `GOOGLE_CLIENT_ID` / `Google:ClientId` | Opcional. Client ID "Aplicación web" de Google Cloud. Vacío = botón de Google desactivado (503). No se usa Client Secret. Guía: `google-oauth.md`. |
| `RateLimiting:Recuperacion` | Solicitar recuperación o establecer contraseña: 5 cada 15 minutos por IP. |
| `SMTP_HOST` / `Correo:Host` | Servidor SMTP. Vacío en Docker local = Mailpit (`http://localhost:8025`). |
| `SMTP_PORT` / `Correo:Puerto` | 587 con Gmail; 1025 con Mailpit. |
| `SMTP_USER`, `SMTP_PASSWORD` / `Correo:Usuario`, `Correo:Password` | En Gmail, una contraseña de aplicación. |
| `SMTP_FROM`, `SMTP_FROM_NAME` / `Correo:Remitente`, `Correo:NombreRemitente` | Remitente "ClimaPlus". |
| `SMTP_SEGURIDAD` / `Correo:Seguridad` | `StartTls` (Gmail), `SslAlConectar` (465) o `Ninguna` (solo Mailpit o localhost). |
| `PROXY_RED_CONOCIDA` / `ProxyInverso:RedesConocidas:0` | Red del proxy HTTPS (H-05), por ejemplo `172.16.0.0/12`. |

En `Production` la API no arranca sin SMTP configurado, ni con `Ninguna` hacia un servidor externo.

## Fase 6: frontend

- React usa todos los endpoints: registro con código, inicio de sesión en dos pasos (el token solo existe después del
  código), recuperación en tres pasos, cambio y creación de contraseña, y "Continuar con Google" (entrar, vincular,
  desvincular) con el botón oficial de Google Identity Services.
- Cabeceras del frontend ajustadas para Google (CSP, `Referrer-Policy`, COOP); las de la API no cambiaron
  (`../seguridad/owasp-top10.md`, A05).
- Pruebas manuales: `../api/pruebas-frontend-autenticacion.md`. Pruebas automáticas: `frontend/src/test`
  (`auth.test.tsx`, `autenticacionSegura.test.tsx`).
- No requiere migraciones nuevas; sigue requiriendo la 004 en la base que use la API.

## Fase 7, Etapa 1: preparación para la base oficial, correo real y hardware

- `GET /health` responde `Unhealthy` si a la base le faltan migraciones del código, y al arrancar la API escribe
  `Base de datos: faltan_migraciones …` en la consola (antes solo se veía un 500 al iniciar sesión). Solo lee
  `__EFMigrationsHistory`; nunca aplica migraciones.
- `appsettings.json`: la sección `Google` estaba anidada por error dentro de `RateLimiting`; vuelve a la raíz.
- `docker-compose.prod.yml`: `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM` y `SMTP_SEGURIDAD` obligatorios en el servidor.
- Respaldos de Docker corregidos (`-f` dentro del contenedor + `docker compose cp`).
- Procedimiento ensayado para la base oficial: [`migracion-004-climaplus_db.md`](migracion-004-climaplus_db.md);
  reversión sin restaurar: `database/scripts/F7_revertir_004.sql`.
- Correo real: [`correo-smtp.md`](correo-smtp.md). Prueba de entrega con Mailpit: `EntregaMailpitTests`
  (se omite sin `MAILPIT_URL`).
- Hardware oficial: ESP32 DevKit con LCD ([`../hardware/plan-integracion-esp32.md`](../hardware/plan-integracion-esp32.md)).
