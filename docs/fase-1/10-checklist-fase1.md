# Checklist FASE 1

**Última revisión:** 2026-10-05
**Leyenda:** `[x]` existe en el proyecto · `[ ]` pendiente

## Planeación
- [x] Problema definido – [`01-descripcion-problema.md`](01-descripcion-problema.md)
- [x] Justificación – [`01-descripcion-problema.md`](01-descripcion-problema.md)
- [x] Objetivo general – [`02-objetivos-alcance.md`](02-objetivos-alcance.md)
- [x] Objetivos específicos – [`02-objetivos-alcance.md`](02-objetivos-alcance.md)
- [x] Alcance – [`02-objetivos-alcance.md`](02-objetivos-alcance.md)

## Arquitectura
- [x] Arquitectura documentada – [`03-arquitectura.md`](03-arquitectura.md)
- [x] Diagrama PlantUML – `docs/arquitectura/arquitectura-climaplus.puml`
- [x] PNG del diagrama – `docs/arquitectura/arquitectura-climaplus.png`

## Base de datos
- [x] Modelo relacional – `docs/base-datos/modelo-relacional.puml` (+ PNG)
- [x] PK
- [x] FK
- [x] Cardinalidades
- [x] Tipos PostgreSQL
- [x] Diccionario de datos – `docs/base-datos/diccionario-datos.md`
- [x] Reglas de negocio – [`05-reglas-negocio.md`](05-reglas-negocio.md)

## Mockups
- [ ] Pantallas públicas (M01–M05)
- [ ] Pantallas usuario (M06–M12)
- [ ] Pantallas administrador (M13–M15)
- [ ] Diseño responsive
- [ ] Enlace Figma
- [ ] Capturas/exportaciones (`docs/mockups/`)

> La especificación de los mockups ya existe ([`08-mockups.md`](08-mockups.md)), pero los diseños en Figma todavía no se han creado.

## Repositorio
- [x] frontend/ (carpeta vacía)
- [x] backend/ (carpeta vacía)
- [x] database/ (`migrations/`, `scripts/`)
- [x] docs/
- [x] hardware/ (`esp32/`)
- [x] README.md
- [x] .gitignore
- [ ] Repositorio GitHub creado

> Las carpetas vacías contienen un archivo `.gitkeep` para que Git las conserve. El proyecto todavía no es un repositorio Git: falta `git init`, el primer commit y la publicación en GitHub.

## Cierre
- [x] D-01 a D-14 documentadas – [`09-decisiones-tecnicas.md`](09-decisiones-tecnicas.md)
- [ ] Documentación revisada (por el equipo / catedrático)
- [ ] FASE 1 v1.0 aprobada

---

## Resumen de estado

| Elemento | Estado |
|---|---|
| Arquitectura | TERMINADA |
| Modelo relacional | TERMINADO (Diseño de datos FASE 1 v1.0) |
| Diccionario de datos | TERMINADO |
| Documentación | ORGANIZADA |
| Mockups Figma | PENDIENTES |
| GitHub | EN PROCESO (estructura lista, repositorio sin crear) |
