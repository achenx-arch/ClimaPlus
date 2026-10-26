# Enmienda E-01 – Perfil de usuario (M16 y M17)

| Campo | Valor |
|---|---|
| Fecha | 2026-10-25 |
| Estado | **Aprobada** por el equipo |
| Fase en la que se implementa | 2D – Autenticación, autorización y perfiles |
| Documentos que amplía | `docs/fase-1/02-objetivos-alcance.md`, `05-reglas-negocio.md`, `08-mockups.md` |
| Documentos que **no** cambia | Modelo relacional, diccionario de datos, D-01 a D-14 |

Esta enmienda **agrega** alcance; no borra ni reemplaza ninguna decisión aprobada. Los documentos de Fase 1 se mantienen intactos y deben leerse junto con este registro.

## 1. Motivo

Los mockups M16 (Mi perfil · Usuario) y M17 (Mi perfil · Administrador) se diseñaron en Figma después de aprobar la Fase 1. La auditoría del 2026-10-25 los marcó como fuera del alcance aprobado (sección 11, decisión 3). El equipo aprobó incorporarlos.

## 2. Requerimientos agregados

| ID | Requerimiento | Mockup | Tabla | Endpoint |
|---|---|---|---|---|
| RF-31 | Consultar el perfil propio: nombre, correo, rol, estado y fecha de registro | M16, M17 | usuario | `GET /api/usuarios/me` |
| RF-32 | Modificar el nombre y el correo propios | M16, M17 | usuario | `PUT /api/usuarios/me` |
| RF-33 | Cambiar la contraseña propia verificando la actual | M16, M17 | usuario | `PUT /api/usuarios/me/password` |

Los tres requerimientos aplican a USUARIO y ADMINISTRADOR. No requieren tablas ni columnas nuevas: usan `usuario.nombre`, `usuario.correo` y `usuario.password_hash`, y solo leen `rol`, `activo` y `fecha_creacion`.

## 3. Reglas de negocio agregadas

La numeración continúa la de `05-reglas-negocio.md`.

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-37 | El rol no se puede modificar desde el perfil. Solo un administrador lo cambia (RF-27). | Backend |
| RN-BD-38 | El estado (`activo`) no se puede modificar desde el perfil. Solo un administrador lo cambia (RF-28). | Backend |
| RN-BD-39 | Cambiar la contraseña exige la contraseña actual correcta, y la nueva debe ser distinta y cumplir la política de contraseña. | Backend |
| RN-BD-40 | El perfil solo opera sobre el usuario del token. No existe forma de consultar ni modificar el perfil de otra persona desde estas rutas. | Backend |
| RN-BD-41 | Al cambiar el correo se aplican RN-BD-01 (único) y RN-BD-04 (minúsculas). | BD + Backend |

## 4. Decisiones técnicas de la Fase 2D

Se tomaron al implementar la Fase 2D porque los documentos aprobados las dejaban abiertas. Están **propuestas**: el equipo debe aprobarlas o pedir cambios. No modifican D-01 a D-14.

| ID | Decisión | Motivo | Estado |
|---|---|---|---|
| D-15 | Política de contraseña: 8 a 128 caracteres, al menos una letra y un número. | `08-mockups.md` dejó la política para el backend. Requisito mínimo razonable, sin reglas que obliguen a contraseñas difíciles de recordar. | PROPUESTA |
| D-16 | JWT HS256 de 60 minutos con los claims `sub` (id_usuario), `role` y `jti`. En cada petición se comprueba en la BD que el usuario siga activo y con el mismo rol. | `07-seguridad-inicial.md` exige rechazar tokens de usuarios desactivados. Al no guardar tokens (D-01), la comprobación debe hacerse por petición. | PROPUESTA |
| D-17 | Rate limiting de 5 peticiones por minuto, por IP y por ruta, en registro, login y cambio de contraseña. | Protección contra fuerza bruta (OWASP). D-14 solo cubría las rutas de clima. | PROPUESTA |
| D-18 | El frontend guarda el JWT en memoria (estado de React), nunca en `localStorage`. | Reducir el impacto de un XSS. Ver la sección 7 de `docs/api/contrato-auth-perfil.md`. | PROPUESTA |
| D-19 | Todo endpoint es privado por defecto; las rutas públicas se marcan explícitamente con `[AllowAnonymous]`. | Seguro por defecto: un endpoint nuevo no queda público por olvido. | PROPUESTA |

## 5. Pendientes que esta enmienda no cubre

- Enmienda de D-05: el entorno local usa PostgreSQL 18.3 en lugar de 16.
- Decisiones 4 y 5 de la auditoría: si el administrador ve "Mi cuenta" y la "Actividad reciente" de M13.
