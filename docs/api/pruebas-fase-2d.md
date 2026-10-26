# Pruebas – Fase 2D (autenticación y perfil)

**Proyecto:** `backend/ClimaPlus.Api.Tests` (xUnit + `WebApplicationFactory` + EF Core InMemory)
**Estado:** escritas, **pendientes de ejecutar**. No se han ejecutado todavía: el entorno donde se escribieron no tiene .NET.

Las pruebas levantan la API completa en memoria: validación, JWT, autorización, rate limiting y manejo de errores son los reales. Solo PostgreSQL se reemplaza por EF Core InMemory, que **no** aplica los CHECK ni los UNIQUE de la base de datos. Esas restricciones se verifican con las pruebas SQL del Integrante 1.

## 1. Matriz de pruebas

| # | Caso pedido | Prueba | Resultado esperado |
|---|---|---|---|
| 1 | Registro válido | `Registro_valido_devuelve_201_con_rol_USUARIO_y_sin_hash` | 201, rol USUARIO, correo en minúsculas, sin hash en la respuesta, hash ≠ contraseña en la BD |
| 2 | Correo duplicado | `Registro_con_correo_duplicado_en_otras_mayusculas_devuelve_409` | 409 con `errors.correo` |
| 3 | Contraseña incorrecta | `Login_con_password_incorrecta_devuelve_401_generico` | 401 con el mismo mensaje si el correo no existe |
| 4 | Login exitoso | `Login_exitoso_devuelve_token_que_abre_auth_me` | 200, token que abre `/api/auth/me` |
| 5 | JWT ausente o inválido | `Endpoint_privado_sin_token_devuelve_401`, `Token_malformado_devuelve_401` (2 casos), `Token_firmado_con_otra_clave_devuelve_401` | 401 `application/problem+json` |
| 6 | Acceso sin permisos | `Usuario_sin_rol_de_administrador_recibe_403`, `Administrador_accede_al_endpoint_de_administrador` | 403 / 200 |
| 7 | Usuario desactivado | `Usuario_desactivado_no_inicia_sesion_y_su_token_previo_deja_de_valer` | Login 403; token anterior 401 |
| 8 | Actualización de perfil | `Actualizar_perfil_cambia_nombre_y_correo_normalizados`, `Actualizar_perfil_con_correo_de_otra_cuenta_devuelve_409`, `Consultar_perfil_devuelve_rol_estado_y_fecha_sin_hash` | 200 normalizado / 409 / 200 |
| 9 | Cambio de contraseña | `Cambiar_password_exige_la_actual_y_luego_solo_vale_la_nueva` | 400 si la actual es incorrecta o igual; 204; la vieja deja de funcionar |
| 10 | Modificar rol y estado | `Intento_de_modificar_rol_o_estado_devuelve_400_y_no_cambia_nada`, `Registro_no_permite_elegir_rol`, `Cambio_de_rol_invalida_el_token_previo`, `No_existe_ruta_para_leer_el_perfil_de_otro_usuario` | 400 sin cambios; 401 con token de rol viejo; 404 |
| 11 | Entradas maliciosas | `Registro_rechaza_nombres_con_codigo` (3), `Correos_maliciosos_se_rechazan_con_400` (3), `Inyeccion_en_la_password_no_abre_sesion`, `Campos_demasiado_largos_se_rechazan`, `Json_invalido_devuelve_400_sin_detalles_internos`, `Registro_con_password_debil_devuelve_400` (3) | 400 / 401, nunca 500 |
| 12 | Rate limiting | `Login_excede_el_limite_y_devuelve_429_con_Retry_After` | 3 intentos con 401; el 4.º recibe 429 con `Retry-After` |

En total son 25 métodos de prueba y 32 casos al contar cada fila de los `[Theory]`.

`/api/pruebas/solo-admin` existe **solo dentro del proyecto de pruebas**. Sirve para probar la autorización por rol mientras no existan los endpoints de administración.

## 2. Configuración local (una vez por integrante)

Desde `backend\ClimaPlus.Api`, en PowerShell:

```powershell
# Clave de firma del JWT: genera 48 bytes aleatorios
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
dotnet user-secrets set "Jwt:Clave" ([Convert]::ToBase64String($bytes))

# Opcional: administrador inicial (se crea al arrancar si no existe ningún administrador)
dotnet user-secrets set "AdminInicial:Nombre" "Administrador ClimaPlus"
dotnet user-secrets set "AdminInicial:Correo" "admin@ejemplo.com"
dotnet user-secrets set "AdminInicial:Password" "<elige-una-contraseña-con-letras-y-numeros>"
```

La cadena de conexión `ConnectionStrings:ClimaPlusDb` ya está configurada desde la Fase 2B. Ningún secreto va en `appsettings.json`.

## 3. Ejecutar

Desde la raíz del repositorio:

```powershell
dotnet sln backend\ClimaPlus.sln add backend\ClimaPlus.Api.Tests\ClimaPlus.Api.Tests.csproj
dotnet restore backend\ClimaPlus.sln
dotnet build backend\ClimaPlus.sln --no-restore
dotnet test backend\ClimaPlus.sln --no-build --logger "console;verbosity=normal"
```

Las pruebas no necesitan PostgreSQL ni User Secrets: usan su propia configuración en memoria.

## 4. Verificación manual con la base de datos real

1. `cd backend\ClimaPlus.Api` y luego `dotnet run --launch-profile https` (si el navegador rechaza el certificado, ejecuta una vez `dotnet dev-certs https --trust`).
2. Abrir `https://localhost:7060/swagger`.
3. Ejecutar `POST /api/auth/register` y `POST /api/auth/login`; copiar el token en el botón **Authorize**.
4. Probar `GET /api/usuarios/me`, `PUT /api/usuarios/me` y `PUT /api/usuarios/me/password`.
5. En psql, comprobar que la contraseña está hasheada:
   `SELECT id_usuario, correo, rol, activo, left(password_hash, 10) AS hash_inicio FROM usuario ORDER BY id_usuario DESC LIMIT 5;`
6. Revisar la consola: deben aparecer eventos `Seguridad:` sin contraseñas ni tokens.

También puedes usar `backend\ClimaPlus.Api\ClimaPlus.Api.http` desde Visual Studio o VS Code.
