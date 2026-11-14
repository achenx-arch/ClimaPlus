# Etapa 2: pasar `climaplus_db` a cuentas reales (respaldo, 004, administrador nuevo y limpieza)

Objetivo: empezar la etapa final con un administrador de correo real, registrar a los demás usuarios por el flujo
normal (correo verificado y login en dos pasos) y quitar las cuentas ficticias de desarrollo **sin tocar** tablas,
migraciones, ubicaciones ni datos de las cuentas que se conservan.

Complementa a [`migracion-004-climaplus_db.md`](migracion-004-climaplus_db.md). **Cada fase marcada 🔒 necesita la
autorización explícita del responsable antes de ejecutarse sobre `climaplus_db`.**

## Scripts

| Script | Qué hace | ¿Escribe? |
|---|---|---|
| `database/scripts/F7_diagnostico_climaplus_db.ps1` | Migraciones aplicadas y faltantes, diferencias con `climaplus_test` | No (solo lectura) |
| `database/scripts/F7_E2_auditoria_usuarios.sql` | Cada cuenta con sus favoritos, reglas, consultas, alertas, dispositivos, lecturas y desafíos; indicios de prueba; referencias cruzadas | No |
| `database/scripts/F7_E2_respaldo_y_ensayo.ps1` | Respaldo verificado de `climaplus_db` → copia `climaplus_db_ensayo` idéntica → 004 en la copia → auditoría → (opcional) limpieza en la copia | Solo en la copia |
| `database/scripts/F7_E2_promover_administrador.sql` | Convierte en ADMINISTRADOR una cuenta real ya registrada y verificada | Por defecto no (ROLLBACK) |
| `database/scripts/F7_E2_limpieza_cuentas.sql` | Informe previo y eliminación transaccional de las cuentas indicadas por id | Por defecto no (ROLLBACK) |

Los dos `.sql` que modifican datos terminan en **ROLLBACK** salvo con `-v confirmar=SI`, y sobre `climaplus_db` exigen
además `-v autorizado_oficial=SI`.

## Hallazgos del código

### Relaciones de `usuario`

| Tabla | Relación | Al borrar el usuario | Qué hace la limpieza |
|---|---|---|---|
| `favorito` | `id_usuario` | CASCADE | Se borra (explícito, para el informe) |
| `configuracion_alerta` | `id_usuario` | CASCADE | Se borra |
| `consulta_clima` | `id_usuario` (opcional desde la 002) | RESTRICT | Se borran las del usuario; las **anónimas** (`id_usuario` nulo) no se tocan |
| `alerta` | `id_usuario`; además `id_consulta` o `id_lectura` | RESTRICT | Se borran las del usuario, antes que sus consultas y lecturas |
| `dispositivo` | `id_usuario` | RESTRICT | Se borra, después de sus lecturas |
| `lectura_sensor` | `id_dispositivo` | RESTRICT | Se borran las de los dispositivos del usuario |
| `desafio_autenticacion`, `identidad_externa` (004) | `id_usuario` | CASCADE | Se borran |
| `ubicacion` | Catálogo compartido | — | **Nunca** se borra |

Las alertas solo se generan con las reglas del dueño de la consulta o del dispositivo (`MotorAlertas`), así que no
debería haber referencias entre cuentas; la auditoría lo comprueba (sección 5) y la limpieza se detiene si las hay.

### Administrador inicial (seeder)

`Security/AdminSeeder.cs` ya existe y es seguro: al arrancar la API crea un ADMINISTRADOR con `AdminInicial:Nombre`,
`AdminInicial:Correo` y `AdminInicial:Password` (User Secrets; en Docker `ADMIN_NOMBRE/ADMIN_CORREO/ADMIN_PASSWORD`)
**solo si no existe ningún administrador**, valida el formato del correo y la política de contraseña, no modifica
cuentas existentes y nunca registra la contraseña. La cuenta queda con `correo_verificado = false`: su primer inicio
de sesión envía un código a ese correo, así que el correo debe ser real.

Limitación para este plan: mientras exista el administrador anterior, **el seeder no actúa**, y el plan exige no
borrar el anterior hasta probar el nuevo. Por eso se usa el **registro normal + promoción** (fase 4). El seeder sigue
siendo la vía para una base nueva (por ejemplo, un volumen de Docker vacío); en ese caso conviene borrar
`AdminInicial:Password` de User Secrets después de crear la cuenta.

### El registro público nunca crea administradores

- `RegistroRequest` rechaza con 400 cualquier campo extra (`rol`, `activo`…) y `AuthService` asigna siempre `USUARIO`.
  Prueba: `AutenticacionTests.Registro_no_permite_elegir_rol`.
- Una cuenta creada con Google es siempre `USUARIO` (prueba en `GoogleTests`).
- El formulario de registro de React no tiene campo de rol.
- No existe ningún endpoint para cambiar roles: la promoción solo puede hacerse con acceso directo a la base.

## Fases

### Fase 1 — Diagnóstico y auditoría (solo lectura, autorizada)

```powershell
cd "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
$env:Path += ";C:\Program Files\PostgreSQL\18\bin"
powershell -ExecutionPolicy Bypass -File database\scripts\F7_diagnostico_climaplus_db.ps1
```

### Fase 2 — Respaldo verificado y ensayo en la copia aislada

Crea `climaplus_db_ensayo` (una base nueva; la oficial solo se lee):

```powershell
powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_respaldo_y_ensayo.ps1
```

Resultado en `Documentos\ClimaPlus-respaldos\etapa2-AAAAMMDD-HHMM`: respaldo `.dump` con su SHA-256, prueba de
restauración idéntica, 004 aplicada en la copia con la huella de datos idéntica y `auditoria-usuarios-copia.txt`.

### Fase 3 — Decidir cuentas (responsable del proyecto)

En `auditoria-usuarios-copia.txt` marca cada cuenta como **eliminar** o **conservar**. La columna `indicios` solo
orienta. Anota los `id_usuario` a eliminar **sin** el administrador anterior (ese se elimina al final).

Ensaya la limpieza en la copia (no confirma nada):

```powershell
powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_respaldo_y_ensayo.ps1 -Reemplazar -IdsLimpieza "2,3" -CantidadLimpieza 2
```

### Fase 4 — Ensayo completo del administrador nuevo en la copia

1. Correo real configurado (`docs/despliegue/correo-smtp.md`).
2. Apunta la API a la copia (sin mostrar la contraseña) y arráncala:

   ```powershell
   cd backend\ClimaPlus.Api
   $linea = dotnet user-secrets list | Where-Object { $_ -like 'ConnectionStrings:ClimaPlusDb = *' }
   $nueva = ($linea -replace '^ConnectionStrings:ClimaPlusDb = ', '') -replace 'Database=[^;]+', 'Database=climaplus_db_ensayo'
   dotnet user-secrets set "ConnectionStrings:ClimaPlusDb" $nueva | Out-Null; Remove-Variable linea, nueva
   dotnet run --launch-profile http
   ```

3. En el frontend, **Crear cuenta** con el correo real del nuevo administrador, contraseña segura, y verifica el código.
4. Promueve (ensayo y luego confirmado, **solo en la copia**):

   ```powershell
   psql -X -U postgres -d climaplus_db_ensayo -v correo=tu.correo@dominio.com -f database\scripts\F7_E2_promover_administrador.sql
   psql -X -U postgres -d climaplus_db_ensayo -v correo=tu.correo@dominio.com -v confirmar=SI -f database\scripts\F7_E2_promover_administrador.sql
   ```

5. Inicia sesión de nuevo (correo, contraseña y código): debe entrar a **Administración** y ver el panel de alertas.
6. Limpieza completa en la copia, ahora sí con el administrador anterior:

   ```powershell
   psql -X -U postgres -d climaplus_db_ensayo -v ids=1,2,3 -v cantidad=3 -v permitir_admin=SI -f database\scripts\F7_E2_limpieza_cuentas.sql
   psql -X -U postgres -d climaplus_db_ensayo -v ids=1,2,3 -v cantidad=3 -v permitir_admin=SI -v confirmar=SI -f database\scripts\F7_E2_limpieza_cuentas.sql
   ```

7. Comprueba con la API: el nuevo administrador sigue entrando; las cuentas borradas reciben 401 al iniciar sesión.

### Fase 5 🔒 — Base oficial (requiere autorización)

Mismo orden que el ensayo, con un respaldo **nuevo** justo antes:

1. Detener la API; respaldo con `F7_E2_respaldo_y_ensayo.ps1 -Reemplazar` (respaldo + verificación).
2. 🔒 Aplicar la 004 (`migracion-004-climaplus_db.md`, paso 5) y verificar.
3. Apuntar la API a `climaplus_db`; registrar el administrador nuevo por la aplicación y verificar el código.
4. 🔒 Promover: `… -d climaplus_db -v correo=… -v confirmar=SI -v autorizado_oficial=SI`.
5. Probar el administrador nuevo (login en dos pasos, panel de administración).
6. 🔒 Limpieza: primero sin `confirmar` (informe), luego con `-v confirmar=SI -v autorizado_oficial=SI`, incluyendo al
   administrador anterior con `-v permitir_admin=SI` **solo** si el paso 5 salió bien.
7. Auditoría final (solo lectura) y `/health` en `Healthy`.

Recuperación en cualquier punto: restaurar el respaldo de la fase 5.1 (`migracion-004-climaplus_db.md`, sección 8).

### Fase 6 — Limpieza del ensayo

Cuando todo esté aprobado: `dropdb -U postgres climaplus_db_ensayo` y volver a apuntar la API a la base que
corresponda. Los respaldos se conservan fuera del repositorio.

## Ensayo realizado en esta etapa (sin acceso a tu servidor)

Los scripts se probaron con PowerShell 7.4 y PostgreSQL 16 en una base simulada con la estructura 001–003 y datos de
muestra (administrador, usuario con dispositivo/lecturas/alertas/consultas/favoritos/reglas, usuario inactivo, una
cuenta "real" y una consulta anónima). Resultados:

| Prueba | Resultado |
|---|---|
| Respaldo → copia | Copia idéntica a la original; 004 aplicada en la copia con huella y roles idénticos |
| Limpieza con un administrador sin `permitir_admin` | Detenida, sin cambios |
| Limpieza con un id inexistente / cantidad distinta | Detenida, sin cambios |
| Eliminar al único administrador | Detenida: no quedaría ningún administrador activo |
| Confirmar en `climaplus_db` sin `autorizado_oficial=SI` | Detenida, sin cambios |
| Promover una cuenta sin correo verificado | Detenida |
| Promover cuenta verificada → limpieza con el administrador anterior | Queda solo el administrador nuevo; ubicaciones, consulta anónima y datos de la cuenta conservada intactos |
| Base "oficial" simulada | Sin ningún cambio (datos idénticos antes y después) |

Falta ejecutarlos en tu Windows con PowerShell 5.1 y PostgreSQL 18 (fases 1 y 2).
