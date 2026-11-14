# Cuenta de administrador con correo real

**Estado:** procedimiento documentado; **no ejecutado** sobre `climaplus_db` (requiere tu aprobación).

## Cómo funciona hoy (auditoría del código)

| Tema | Realidad en el código |
|---|---|
| Roles | `USUARIO` y `ADMINISTRADOR` (`Domain/Constantes/ValoresDominio.cs`), columna `usuario.rol` con CHECK |
| Registro público | `POST /api/auth/register` crea siempre `USUARIO` (`AuthService.cs`, RN-BD-05). El DTO rechaza campos extra (`JsonUnmappedMemberHandling.Disallow`), así que no se puede enviar `rol` |
| Google | Las cuentas nuevas por Google son siempre `USUARIO` (`AuthService.Google.cs`) |
| Perfil | `PUT /api/usuarios/me` solo cambia nombre y correo; nunca rol ni estado (RN-BD-37/38) |
| Verificación de correo | Código de 6 dígitos (`VERIFICAR_CORREO`); el inicio de sesión de una cuenta sin verificar pide ese código antes del 2FA |
| 2FA | Todo inicio de sesión con contraseña requiere el código `INICIO_SESION` por correo; también para administradores |
| Primer administrador | `AdminSeeder`: solo si `AdminInicial:*` está configurado **y no existe ningún administrador**. Como ya existe uno, no actúa |
| Promoción | El primer administrador, solo con el script `database/scripts/F7_E2_promover_administrador.sql`. Desde M14, un administrador activo puede cambiar el rol de **otra** cuenta con `PATCH /api/admin/usuarios/{id}/rol` (`contrato-admin-usuarios.md`) |
| Cambio de rol en caliente | Cada petición compara el rol del JWT con la base (`rol_cambiado` → 401): al promover, la sesión se cierra y el nuevo token lleva el rol nuevo |
| Panel administrativo | Rutas `/admin*` del frontend protegidas por rol; en la API, `GET /api/admin/alertas` y `GET /api/admin/alertas/{id}` con `[Authorize(Roles = "ADMINISTRADOR")]`. Gestión de usuarios (M14): `GET /api/admin/usuarios` y los `PATCH` de rol y estado, con el mismo atributo. Nadie puede quitarse su propio rol ni desactivarse, y siempre queda un administrador activo (RN-BD-09) |

**Riesgo de escalada:** no se encontró ninguna vía para que un usuario normal obtenga `ADMINISTRADOR` desde la API.
Las pruebas existentes cubren el registro enviando `rol: ADMINISTRADOR` (rechazado) y un token con rol forjado con otra clave (rechazado).

## Procedimiento recomendado (reutiliza el sistema existente, sin contraseñas en texto plano)

1. **Registrar la cuenta nueva por el flujo normal** en el frontend, con el correo real de administración y una
   contraseña que cumpla la política (8–128, con letras y números). La contraseña la escribe solo su dueño; se guarda
   con hash (PBKDF2 de ASP.NET Identity).
2. **Verificar el correo** con el código que llega (o al primer inicio de sesión).
3. **Iniciar sesión una vez** (contraseña + código 2FA) para confirmar que la cuenta funciona.
4. **Ensayo** (no cambia nada, termina en ROLLBACK):
   ```powershell
   $env:Path = "C:\Program Files\PostgreSQL\18\bin;" + $env:Path
   psql -X -h localhost -U postgres -d climaplus_db -v correo=<correo-admin> -f database\scripts\F7_E2_promover_administrador.sql
   ```
   Debe mostrar la cuenta como `USUARIO`, activa, verificada y con contraseña local, y terminar en `== ENSAYO`.
5. **Confirmar** (con tu aprobación; respaldo previo con `pg_dump -Fc`):
   ```powershell
   psql -X -h localhost -U postgres -d climaplus_db -v correo=<correo-admin> -v confirmar=SI -v autorizado_oficial=SI -f database\scripts\F7_E2_promover_administrador.sql
   ```
   Solo cambia esa fila: `rol = ADMINISTRADOR` y un sello de seguridad nuevo. No toca otros usuarios.
6. Entrar de nuevo (contraseña + código) y comprobar el panel `/admin`.

En Azure, la cuenta llega con la copia de la base. Si se crea después del despliegue, los pasos 4–5 se ejecutan dentro
del contenedor:
`docker compose --env-file .env.azure -f docker-compose.azure.yml exec -T db psql -U postgres -d climaplus_db -v correo=<correo> -f - < database/scripts/F7_E2_promover_administrador.sql`
(con `-v confirmar=SI -v autorizado_oficial=SI` para confirmar).
