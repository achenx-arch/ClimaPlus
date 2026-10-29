# Mockups – Especificación para Figma

**Estado:** especificación lista · **Diseño en Figma: PENDIENTE**
**Enlace Figma:** _pendiente: se agregará cuando se cree el archivo_
**Exportaciones:** se guardarán en [`../mockups/`](../mockups/)

Este documento define qué pantallas diseñar, qué contiene cada una y con qué estilo. Sirve como guía antes de abrir Figma y, más adelante, antes de programar los componentes en React. No contiene código.

---

## Parte A – Sistema visual

### Concepto

**Clima · moderna · limpia · académica · responsive.**

La interfaz es clara, con mucho espacio en blanco, un fondo gris azulado muy suave y tarjetas blancas. El azul cielo es el color de marca. Los colores fuertes se reservan para las alertas, de modo que destaquen. La temperatura es el dato protagonista. No debe parecer un panel empresarial: pocas columnas, pocos colores e iconos sencillos.

Todo se puede reproducir con **Bootstrap 5**: grilla, `card`, `navbar`, `offcanvas`, `table`, `badge`, `alert`, `toast`, `form-control`, `form-switch`, `modal` y `placeholder`, además de **Bootstrap Icons** para la iconografía.

### Colores principales

| Token | Hex | Uso | Variable Bootstrap sugerida |
|---|---|---|---|
| Primario – Azul cielo | `#1E6FD9` | Botones principales, enlaces, ítem activo de la barra lateral | `--bs-primary` |
| Primario oscuro | `#1557B0` | Hover/active del botón primario, títulos destacados | — |
| Secundario – Celeste | `#38BDF8` | Acentos, degradado del encabezado, gráficos | `--bs-info` |
| Fondo | `#F4F8FB` | Fondo general de la página | `--bs-body-bg` |
| Superficie | `#FFFFFF` | Tarjetas, navbar, tablas, modales | — |
| Texto principal | `#1E293B` | Texto general y títulos | `--bs-body-color` |
| Texto secundario | `#64748B` | Etiquetas, fechas, descripciones | `--bs-secondary-color` |
| Borde | `#E2E8F0` | Bordes de tarjetas, inputs y tablas | `--bs-border-color` |

**Degradado del encabezado (hero / WeatherCard principal):** `#1E6FD9 → #38BDF8`, a 135°.

### Colores secundarios y de estado

| Token | Hex | Uso | Bootstrap |
|---|---|---|---|
| Éxito | `#16A34A` | Dispositivo en línea, mensajes de éxito, usuario activo | `success` |
| Advertencia | `#F59E0B` | Avisos, límite de peticiones alcanzado | `warning` (texto oscuro) |
| Peligro | `#DC2626` | Errores, acciones destructivas (eliminar, desactivar) | `danger` |
| Neutro | `#94A3B8` | Inactivo, fuera de línea, deshabilitado | `secondary` |

### Colores por tipo de alerta

| Tipo | Color | Icono (Bootstrap Icons) |
|---|---|---|
| `TEMPERATURA_ALTA` | Naranja `#EA580C` | `bi-thermometer-sun` |
| `TEMPERATURA_BAJA` | Cian `#0891B2` | `bi-thermometer-snow` |
| `LLUVIA` | Índigo `#4F46E5` | `bi-cloud-rain` |

El color nunca es la única señal: cada alerta lleva además su icono y el nombre del tipo.

### Tipografía

- **Fuente:** Inter (Google Fonts). Alternativa: la pila del sistema de Bootstrap (`system-ui`, Segoe UI, Roboto…).
- **Números de temperatura:** Inter con `font-variant-numeric: tabular-nums`, para que las cifras no "bailen".

| Estilo | Tamaño | Peso | Uso |
|---|---|---|---|
| Temperatura principal | 56 px (40 px en móvil) | 300 (light) | Dato grande de la WeatherCard |
| H1 | 32 px | 700 | Título de página |
| H2 | 24 px | 600 | Título de sección |
| H3 | 20 px | 600 | Título de tarjeta |
| Cuerpo | 16 px | 400 | Texto general |
| Pequeño | 14 px | 400 | Etiquetas, fechas, tablas |
| Micro | 12 px | 500 | Badges, ayudas de formulario |

### Espaciado, bordes y sombras

- **Retícula de 8 px:** márgenes y rellenos en múltiplos de 8 (clases `p-2`, `p-3`, `p-4`, `gap-3`…).
- **Radios:** tarjetas 12 px, botones e inputs 8 px, badges en forma de píldora.
- **Sombra de tarjeta:** muy suave (`0 1px 3px rgba(15, 23, 42, .08)`, similar a `shadow-sm`).
- **Ancho máximo del contenido:** 1200 px (`container-xl`).

### Botones

| Variante | Estilo | Uso |
|---|---|---|
| Primario | Relleno `#1E6FD9`, texto blanco | Acción principal: Buscar, Guardar, Iniciar sesión |
| Secundario | Borde `#1E6FD9`, fondo transparente (`btn-outline-primary`) | Acciones secundarias: Ver pronóstico, Ver detalle |
| Peligro | `btn-outline-danger`, que pasa a relleno al confirmar | Eliminar favorito, desactivar |
| Enlace | `btn-link` | Acciones menores: "¿No tienes cuenta? Regístrate" |
| Icono | Botón cuadrado de 40 px con icono | Favorito ★, copiar API Key, menú |

- **Altura:** 40 px (tamaño normal) y 48 px en el botón de búsqueda del hero.
- **Estados a dibujar en Figma:** normal, hover, deshabilitado y cargando (con *spinner*).

### Inputs

- **Medidas:** altura de 40 px, borde `#E2E8F0`, radio de 8 px, etiqueta encima en 14 px y peso 500.
- **Foco:** borde primario con un anillo suave del color primario al 25 %.
- **Error:** borde `#DC2626` con mensaje debajo en 12 px rojo (`is-invalid` + `invalid-feedback`).
- **Buscador de ubicación:** input grande (48 px) con icono `bi-search` y una lista desplegable de resultados con nombre, región y país.
- **Interruptores:** `form-switch` para "Regla activa" y "Dispositivo activo".

### Tarjetas

Base común: fondo blanco, borde de 1 px `#E2E8F0`, radio de 12 px, sombra suave y relleno de 24 px (16 px en móvil).

### Navbar

- **Pública:** logo "☁ ClimaPlus" a la izquierda, buscador compacto en el centro (oculto en móvil) y, a la derecha, "Iniciar sesión" (enlace) y "Registrarse" (botón primario).
- **Autenticada:** logo, buscador compacto, campana `bi-bell` con contador de alertas `NUEVA` (badge rojo) y menú del usuario (nombre, rol y "Cerrar sesión").
- **Medidas:** altura de 64 px, fondo blanco con borde inferior y posición fija arriba.

### Sidebar (usuario y administrador)

- **Medidas:** ancho de 240 px, fondo blanco y borde derecho.
- **Ítems:** icono + texto; el activo tiene fondo azul claro (`#E8F1FC`) y texto primario.
- **Menos de 992 px (`lg`):** se convierte en un `offcanvas` que se abre con un botón hamburguesa en la navbar.

| Usuario | Administrador |
|---|---|
| `bi-speedometer2` Inicio | **Administración** |
| `bi-star` Favoritos | `bi-graph-up` Panel administrador |
| `bi-clock-history` Historial | `bi-people` Usuarios |
| `bi-bell` Alertas | `bi-exclamation-triangle` Alertas del sistema |
| `bi-sliders` Configurar alertas | **Mi cuenta** |
| `bi-cpu` Mis dispositivos | (los mismos ítems del usuario) |

### Componentes del dominio

#### WeatherCard (clima actual)

- **Encabezado:** nombre de la ubicación, región, país e icono ★ de favorito (solo con sesión).
- **Cuerpo:** icono del clima de 64 px, temperatura principal de 56 px y descripción ("lluvia ligera").
- **Datos secundarios en fila:** sensación térmica, humedad (`bi-droplet`) y viento en m/s (`bi-wind`).
- **Pie:** "Consultado a las 14:32".
- **Variantes:** grande (pantalla de resultado, con fondo degradado y texto blanco) y compacta (favoritos y dashboard, en blanco).

**Iconos del clima:** se toman de Bootstrap Icons según `codigo_condicion`. El frontend **no** descarga imágenes desde OpenWeather, porque no se comunica con OpenWeather.

| Código | Condición | Icono |
|---|---|---|
| 2xx | Tormenta | `bi-cloud-lightning-rain` |
| 3xx | Llovizna | `bi-cloud-drizzle` |
| 5xx | Lluvia | `bi-cloud-rain-heavy` |
| 6xx | Nieve | `bi-cloud-snow` |
| 7xx | Neblina / niebla | `bi-cloud-fog` |
| 800 | Despejado | `bi-sun` (`bi-moon` de noche) |
| 801–804 | Nubes | `bi-cloud-sun` / `bi-clouds` |

#### ForecastCard (pronóstico)

- **Contenido:** día ("Mié 7"), icono, descripción breve y temperaturas máxima / mínima.
- **Medidas:** tarjeta estrecha (≈ 140 px); se muestran 5 en fila.
- **Interacción:** al seleccionarla se resalta con un borde primario y debajo se muestran los bloques de 3 horas (hora, icono, temperatura, humedad, viento).

#### AlertCard

- **Señal del tipo:** borde izquierdo de 4 px con el color del tipo de alerta, más su icono.
- **Contenido:** el `mensaje` de la alerta (texto principal), la ubicación y la fecha/hora.
- **Badges:** origen ("OpenWeather" o "ESP32") y estado (`NUEVA` en primario, `LEIDA` en gris).
- **Acción:** "Marcar como leída" (solo si es `NUEVA`).
- **Lectura visual:** las alertas leídas se muestran con opacidad reducida.

#### DeviceCard

- **Contenido:** icono `bi-cpu`, nombre del dispositivo y ubicación.
- **Badges:** "Activo / Inactivo" y un punto de conexión: verde = en línea, gris = fuera de línea, calculado a partir de `ultima_conexion`.
- **Datos:** "Última conexión: hace 3 min" (o "Nunca conectado") y resumen de la última lectura (temperatura, humedad, lluvia sí/no, luz %).
- **Acciones:** Ver detalle, Editar, Activar/Desactivar.

#### Tablas administrativas

- **Estilo:** `table table-hover align-middle` dentro de una tarjeta; encabezado con fondo `#F8FAFC`, texto de 14 px y fila de 48 px de alto.
- **Filtros:** encima de la tabla, en una fila (buscador, selects y rango de fechas).
- **Paginación:** abajo a la derecha, 20 filas por página.
- **Formato:** estados y tipos como badges; acciones con iconos y *tooltip* al final de la fila.
- **Móvil:** la tabla se envuelve en `table-responsive`, o se convierte en una lista de tarjetas.

### Estados vacíos

- **Estructura:** icono de 48 px en `#94A3B8`, título de 20 px, una línea explicativa y un botón de acción.
- **Disposición:** todo centrado dentro de la tarjeta.

| Pantalla | Título | Acción |
|---|---|---|
| Favoritos | "Aún no tienes favoritos" | Buscar una ubicación |
| Historial | "Todavía no has consultado el clima" | Buscar una ubicación |
| Alertas | "Sin alertas por ahora" | Configurar alertas |
| Configurar alertas | "No tienes reglas de alerta" | Crear regla |
| Mis dispositivos | "No tienes dispositivos registrados" | Registrar dispositivo |
| Detalle dispositivo | "Este dispositivo aún no envía lecturas" | (sin acción) |
| Búsqueda | "No encontramos esa ubicación" | Revisar la ortografía |

### Mensajes de error y retroalimentación

| Tipo | Componente | Ejemplo |
|---|---|---|
| Error de campo | `invalid-feedback` bajo el input | "Ingresa un correo válido" |
| Error de formulario / servidor | `alert alert-danger` arriba del formulario | "Correo o contraseña incorrectos" |
| Cuenta inactiva | `alert alert-warning` | "Tu cuenta está desactivada. Contacta al administrador." |
| Servicio meteorológico no disponible | `alert alert-warning` en la tarjeta | "No pudimos obtener el clima en este momento. Intenta de nuevo." |
| Demasiadas peticiones (D-14) | `alert alert-warning` | "Has hecho muchas consultas seguidas. Espera un momento." |
| Sesión expirada | Toast y redirección a M02 | "Tu sesión expiró. Inicia sesión de nuevo." |
| Éxito | Toast verde abajo a la derecha (3 s) | "Ubicación agregada a favoritos" |
| Confirmación destructiva | `modal` | "¿Eliminar Cobán de tus favoritos?" |
| Cargando | `spinner-border` en botones y `placeholder-glow` en tarjetas | — |

### Diseño responsive

| Breakpoint Bootstrap | Ancho | Comportamiento |
|---|---|---|
| xs | < 576 px | Una columna. Navbar con logo + hamburguesa. ForecastCards con desplazamiento horizontal. Tablas como tarjetas o con scroll. |
| sm / md | 576–991 px | Tarjetas en 2 columnas (`col-md-6`). Sidebar en `offcanvas`. |
| lg | ≥ 992 px | Sidebar fija de 240 px. Tarjetas en 3 columnas (`col-lg-4`). |
| xl | ≥ 1200 px | Contenido centrado con un ancho máximo de 1200 px. |

**Frames para Figma:** escritorio de **1440 × 1024** y móvil de **390 × 844**. Se diseña primero escritorio y después la versión móvil de M01, M04, M05, M06 y M09.

### Organización sugerida del archivo Figma

| Página Figma | Contenido |
|---|---|
| 00 · Sistema visual | Colores, tipografía, botones, inputs, badges, tarjetas e iconos, creados como **componentes** |
| 01 · Públicas | M01 – M05 |
| 02 · Usuario | M06 – M12 |
| 03 · Administrador | M13 – M15 |
| 04 · Móvil | Versiones de 390 px |
| 05 · Prototipo | Conexiones de navegación |

---

## Parte B – Pantallas

### Resumen

| Código | Pantalla | Rol | Layout |
|---|---|---|---|
| M01 | Inicio / Dashboard público | Público | Navbar pública |
| M02 | Iniciar sesión | Público | Centrado |
| M03 | Registro | Público | Centrado |
| M04 | Resultado clima actual | Público / Usuario | Navbar (pública o autenticada) |
| M05 | Pronóstico | Público / Usuario | Navbar (pública o autenticada) |
| M06 | Dashboard usuario | Usuario | Navbar + sidebar |
| M07 | Favoritos | Usuario | Navbar + sidebar |
| M08 | Historial | Usuario | Navbar + sidebar |
| M09 | Alertas | Usuario | Navbar + sidebar |
| M10 | Configuración de alertas | Usuario | Navbar + sidebar |
| M11 | Mis dispositivos | Usuario | Navbar + sidebar |
| M12 | Detalle dispositivo | Usuario | Navbar + sidebar |
| M13 | Dashboard administrador | Administrador | Navbar + sidebar admin |
| M14 | Gestión de usuarios | Administrador | Navbar + sidebar admin |
| M15 | Gestión / consulta de alertas | Administrador | Navbar + sidebar admin |

> El administrador también tiene acceso a M06–M12 para su propia cuenta.

---

### Pantallas públicas

#### M01 – Inicio / Dashboard público

| Campo | Detalle |
|---|---|
| **Rol** | Público (también accesible con sesión) |
| **Objetivo** | Buscar una ubicación y conocer qué ofrece ClimaPlus |
| **Componentes** | Navbar pública; hero con degradado y título "¿Cómo está el clima hoy?"; buscador grande con lista de resultados; 3 tarjetas informativas (Clima y pronóstico · Favoritos e historial · Alertas personalizadas); botón "Crear cuenta"; pie de página |
| **Datos mostrados** | Resultados de búsqueda: nombre, región y país de cada coincidencia |
| **Acciones** | Escribir y buscar; elegir un resultado; ir a login o registro |
| **Navegación** | Resultado elegido → M04 · "Iniciar sesión" → M02 · "Registrarse" / "Crear cuenta" → M03 |

#### M02 – Iniciar sesión

| Campo | Detalle |
|---|---|
| **Rol** | Público |
| **Objetivo** | Autenticar al usuario |
| **Componentes** | Tarjeta centrada (máx. 420 px) con logo; inputs de correo y contraseña (con botón mostrar/ocultar); botón "Iniciar sesión"; enlace a registro; zona de alerta de error |
| **Datos mostrados** | Mensajes de error: credenciales incorrectas, cuenta inactiva, sesión expirada |
| **Acciones** | Iniciar sesión |
| **Navegación** | USUARIO → M06 · ADMINISTRADOR → M13 · "Regístrate" → M03 · Logo → M01 |

#### M03 – Registro

| Campo | Detalle |
|---|---|
| **Rol** | Público |
| **Objetivo** | Crear una cuenta con rol USUARIO |
| **Componentes** | Tarjeta centrada; inputs de nombre, correo, contraseña y confirmar contraseña; botón "Crear cuenta"; enlace a login |
| **Datos mostrados** | Errores por campo; error "Ese correo ya está registrado" (RN-BD-01) |
| **Acciones** | Registrarse |
| **Navegación** | Éxito → M02 con el mensaje "Cuenta creada, inicia sesión" · "Ya tengo cuenta" → M02 |

> La política de contraseña (longitud mínima, etc.) se definirá al implementar el backend. En el mockup basta con una ayuda bajo el campo.

#### M04 – Resultado clima actual

| Campo | Detalle |
|---|---|
| **Rol** | Público y usuario autenticado |
| **Objetivo** | Mostrar el clima actual de la ubicación elegida |
| **Componentes** | Navbar (según sesión); buscador compacto; **WeatherCard grande**; botón "Ver pronóstico de 5 días"; botón ★ "Guardar en favoritos" |
| **Datos mostrados** | Ubicación, región, país, temperatura, sensación térmica, humedad, viento, descripción, icono y hora de la consulta |
| **Acciones** | Ver pronóstico; guardar en favoritos (con sesión) o "Inicia sesión para guardar favoritos" (sin sesión); hacer una nueva búsqueda |
| **Navegación** | → M05 · ★ sin sesión → M02 · Logo → M01 o M06 |

> Toda consulta se guarda como `ACTUAL` antes de mostrarse; sin sesión, con `id_usuario` NULL y fuera de cualquier historial (E-02). Con sesión, entra en el historial del usuario y puede generar alertas. Si se genera una, se muestra un toast "Nueva alerta: …" y aumenta el contador de la campana. No se muestra índice UV: requiere un plan de pago de OpenWeather.

#### M05 – Pronóstico

| Campo | Detalle |
|---|---|
| **Rol** | Público y usuario autenticado |
| **Objetivo** | Mostrar el pronóstico de 5 días de la ubicación |
| **Componentes** | Navbar; encabezado con la ubicación; fila de **5 ForecastCards**; panel de bloques de 3 horas del día seleccionado (lista o tabla); botón "Volver al clima actual"; botón ★ favorito |
| **Datos mostrados** | Por día: máxima, mínima, icono y descripción. Por bloque: hora, icono, temperatura, humedad y viento |
| **Acciones** | Seleccionar un día; volver a M04; guardar favorito |
| **Navegación** | → M04 · ★ sin sesión → M02 |

> Toda consulta se guarda como `PRONOSTICO`, con los días y bloques normalizados en `detalle` (E-03) y sin mediciones en columnas; sin sesión, con `id_usuario` NULL (E-02). El pronóstico nunca genera alertas.

---

### Pantallas de usuario

#### M06 – Dashboard usuario

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO (y ADMINISTRADOR en su cuenta) |
| **Objetivo** | Resumen de la actividad del usuario y acceso rápido |
| **Componentes** | Navbar autenticada; sidebar; saludo "Hola, {nombre}"; buscador; 3 tarjetas resumen (alertas nuevas, favoritos, dispositivos activos); lista de favoritos en formato compacto; últimas 3 AlertCards; resumen de dispositivos |
| **Datos mostrados** | Conteo de alertas `NUEVA`, de favoritos y de dispositivos activos; para cada favorito, **la última consulta registrada** (temperatura, condición y hora); alertas recientes; estado de los dispositivos |
| **Acciones** | Buscar; "Ver clima ahora" en un favorito; ir a cada módulo |
| **Navegación** | → M04, M07, M09, M11 |

> Para no llenar el historial ni gastar cuota de OpenWeather, el dashboard **no** consulta automáticamente el clima de cada favorito. Muestra la última fotografía guardada y ofrece "Ver clima ahora".

#### M07 – Favoritos

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO |
| **Objetivo** | Gestionar las ubicaciones favoritas |
| **Componentes** | Título + botón "Agregar ubicación" (abre el buscador); grilla de tarjetas de favorito; modal de confirmación para eliminar; estado vacío |
| **Datos mostrados** | Nombre, región, país, fecha en que se agregó y última consulta registrada |
| **Acciones** | Ver clima, ver pronóstico, configurar alerta para esa ubicación, eliminar favorito |
| **Navegación** | → M04 · → M05 · → M10 (ubicación preseleccionada) |

#### M08 – Historial

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO |
| **Objetivo** | Consultar el historial de consultas meteorológicas |
| **Componentes** | Filtros: tipo (Todos / Actual / Pronóstico), ubicación y rango de fechas; tabla paginada; estado vacío |
| **Datos mostrados** | Fecha/hora, ubicación, tipo (badge), temperatura, condición, humedad y viento. Las filas `PRONOSTICO` muestran "—" en las mediciones |
| **Acciones** | Filtrar, paginar, "Consultar de nuevo" |
| **Navegación** | "Consultar de nuevo" → M04 (Actual) o M05 (Pronóstico) |

#### M09 – Alertas

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO |
| **Objetivo** | Revisar las alertas recibidas |
| **Componentes** | Filtros por estado (Todas / Nuevas / Leídas) y tipo; lista de AlertCards; paginación; botón "Configurar alertas"; estado vacío |
| **Datos mostrados** | Mensaje, tipo, ubicación, valor detectado y umbral (temperatura), origen (OpenWeather / ESP32), estado y fecha |
| **Acciones** | Marcar como leída; filtrar |
| **Navegación** | → M10 · origen ESP32 → M12 de ese dispositivo |

#### M10 – Configuración de alertas

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO |
| **Objetivo** | Crear y administrar reglas de alerta |
| **Componentes** | Recuadro informativo (Temp. alta: avisa si supera el umbral · Temp. baja: avisa si baja del umbral · Lluvia: avisa si se detecta lluvia); tabla/lista de reglas; botón "Nueva regla"; modal con el formulario |
| **Datos mostrados** | Por regla: tipo, ubicación ("Todas mis ubicaciones" si es global), umbral en °C (— para lluvia), activa sí/no, fecha de creación |
| **Formulario** | Tipo (select) · Ubicación (Todas / favoritos / buscar) · Umbral °C (deshabilitado si el tipo es Lluvia) · Activa (switch) |
| **Validaciones visibles** | Umbral requerido y entre -50 y 60 °C; regla duplicada (RN-BD-19); alta debe ser mayor que baja (RN-BD-21) |
| **Acciones** | Crear, editar, activar/pausar con el switch, eliminar (con confirmación) |
| **Navegación** | → M09 |

#### M11 – Mis dispositivos

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO |
| **Objetivo** | Ver y registrar dispositivos ESP32 |
| **Componentes** | Título + botón "Registrar dispositivo"; grilla de DeviceCards; modal de registro (nombre y ubicación); **modal de API Key** que se muestra una sola vez, con botón copiar y aviso "Guárdala ahora; no podrás verla de nuevo"; estado vacío |
| **Datos mostrados** | Nombre, ubicación, activo/inactivo, en línea/fuera de línea, última conexión y resumen de la última lectura |
| **Acciones** | Registrar, ver detalle, editar nombre/ubicación, activar/desactivar |
| **Navegación** | → M12 |

#### M12 – Detalle dispositivo

| Campo | Detalle |
|---|---|
| **Rol** | USUARIO (dueño) |
| **Objetivo** | Revisar las lecturas y alertas de un dispositivo |
| **Componentes** | Encabezado con nombre, ubicación, badges de estado y última conexión; 4 tarjetas pequeñas con la última lectura (temperatura, humedad, lluvia, luz); tabla de lecturas recientes paginada; lista de alertas originadas por el dispositivo; botones Editar y Activar/Desactivar |
| **Datos mostrados** | Lecturas: fecha/hora, temperatura, humedad, lluvia (Sí/No) y luz %. Alertas con origen en este dispositivo |
| **Acciones** | Editar, activar/desactivar (con confirmación), paginar lecturas |
| **Navegación** | "Volver" → M11 · alerta → M09 |

---

### Pantallas de administrador

#### M13 – Dashboard administrador

| Campo | Detalle |
|---|---|
| **Rol** | ADMINISTRADOR |
| **Objetivo** | Ver estadísticas básicas del sistema |
| **Componentes** | Sidebar de administración; selector de periodo (Hoy / 7 días / 30 días); 4 tarjetas KPI; tabla "Ubicaciones más consultadas" (top 5); tabla de alertas recientes; *(opcional)* gráfico de barras sencillo de consultas por día |
| **Datos mostrados** | Usuarios activos / total; consultas del periodo (Actual vs Pronóstico); alertas del periodo por tipo; dispositivos activos; top 5 ubicaciones |
| **Acciones** | Cambiar periodo; ir a usuarios o alertas |
| **Navegación** | → M14 · → M15 |

#### M14 – Gestión de usuarios

| Campo | Detalle |
|---|---|
| **Rol** | ADMINISTRADOR |
| **Objetivo** | Administrar las cuentas de usuario |
| **Componentes** | Buscador (nombre/correo); filtros por rol y estado; tabla paginada; modal "Cambiar rol"; modal de confirmación para activar/desactivar |
| **Datos mostrados** | Nombre, correo, rol (badge), estado (badge), fecha de registro |
| **Acciones** | Cambiar rol; activar/desactivar. Si se intenta desactivar o degradar al último administrador activo, se muestra un error (RN-BD-09) |
| **Navegación** | → M13 · → M15 |

> No existe borrado físico de usuarios (RN-BD-08), así que no se dibuja un botón "Eliminar".

#### M15 – Gestión / consulta de alertas

| Campo | Detalle |
|---|---|
| **Rol** | ADMINISTRADOR |
| **Objetivo** | Consultar las alertas generadas en todo el sistema |
| **Componentes** | Filtros: tipo, estado, origen (OpenWeather / ESP32), rango de fechas, usuario; tabla paginada; modal de detalle |
| **Datos mostrados** | Fecha, usuario, ubicación, tipo, valor detectado / umbral, origen, estado y mensaje (truncado en la tabla, completo en el detalle) |
| **Acciones** | Filtrar, paginar, ver detalle (solo lectura) |
| **Navegación** | → M13 · → M14 |

---

### Mapa de navegación

```
M01 Inicio público ──buscar──► M04 Clima actual ◄──► M05 Pronóstico
 │                                │ ★ sin sesión
 ├─► M02 Login ◄──────────────────┘
 │     ├─ USUARIO ───────► M06 Dashboard usuario
 │     └─ ADMINISTRADOR ─► M13 Dashboard admin
 └─► M03 Registro ──éxito──► M02

M06 Dashboard usuario
 ├─► M07 Favoritos ──► M04 / M05 / M10
 ├─► M08 Historial ──► M04 / M05
 ├─► M09 Alertas ◄──► M10 Configuración de alertas
 └─► M11 Mis dispositivos ──► M12 Detalle dispositivo

M13 Dashboard admin ◄──► M14 Usuarios ◄──► M15 Alertas del sistema
(el administrador también accede a M06–M12 desde "Mi cuenta")
```

---

## Parte C – Wireframes textuales

Escala aproximada de escritorio. Leyenda: `[ ]` botón o input · `( )` badge · `[*]` favorito · `v` select · `(!)2` campana con 2 alertas nuevas · `>` ítem activo del sidebar · `o` punto de conexión. Los iconos se indican con texto entre corchetes; en Figma se reemplazan por Bootstrap Icons.

### 1. Dashboard público (M01)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]   Login [Registrarse]                |
+----------------------------------------------------------------------------------+
| ///////////////////////////// fondo degradado azul ///////////////////////////// |
|                                                                                  |
|                    ¿Cómo está el clima hoy?                                      |
|          Consulta el clima actual y el pronóstico de 5 días                      |
|                                                                                  |
|      [ (lupa) Cobán                                  ] [ Buscar ]                |
|      +-------------------------------------------+                               |
|      | Cobán, Alta Verapaz, GT                   |                               |
|      | Cobán, Petén, GT                          |                               |
|      +-------------------------------------------+                               |
| //////////////////////////////////////////////////////////////////////////////   |
|                                                                                  |
| +--------------------+  +--------------------+  +--------------------+           |
| | Clima y pronóstico |  | Favoritos e        |  | Alertas            |           |
| | sin registrarte    |  | historial          |  | personalizadas     |           |
| |                    |  | Guarda tus ciudades|  | Lluvia, calor, frío|           |
| +--------------------+  +--------------------+  +--------------------+           |
|                                                                                  |
|               ¿Quieres más?   [ Crear cuenta gratis ]                            |
+----------------------------------------------------------------------------------+
| ClimaPlus - Proyecto Desarrollo Web - UMG                                        |
+----------------------------------------------------------------------------------+
```

### 2. Resultado clima actual (M04)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]        (!)2  Ana v                   |
+----------------------------------------------------------------------------------+
|                                                                                  |
| +--------------------------------------------------------------------+           |
| | ////////////// WeatherCard grande (fondo degradado) ////////////// |           |
| | Cobán, Alta Verapaz - GT                                     [*]   |           |
| |                                                                    |           |
| |    [icono lluvia]      22 C                                        |           |
| |                        lluvia ligera                               |           |
| |                                                                    |           |
| | Sensación 23 C   |   Humedad 88 %   |   Viento 2.1 m/s             |           |
| |                                            Consultado a las 14:32  |           |
| +--------------------------------------------------------------------+           |
|                                                                                  |
|      [ Ver pronóstico de 5 días ]     [ * Guardar en favoritos ]                 |
|                                                                                  |
|  Sin sesión el botón dice: "Inicia sesión para guardar favoritos"                |
+----------------------------------------------------------------------------------+
```

### 3. Dashboard usuario (M06)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]        (!)2  Ana v                   |
+----------------------------------------------------------------------------------+
| > Inicio           | Hola, Ana                                                   |
|   Favoritos        | [ (lupa) Buscar ubicación...                ] [Buscar]      |
|   Historial        |                                                             |
|   Alertas          | +-------------+  +-------------+  +-------------+           |
|   Config. alertas  | | 2 alertas   |  | 4 favoritos |  | 1 / 1       |           |
|   Mis dispositivos | | nuevas      |  |             |  | disp. activo|           |
|                    | +-------------+  +-------------+  +-------------+           |
|                    |                                                             |
|                    | Mis favoritos                              [Ver todos]      |
|                    | +-----------------------+  +-----------------------+        |
|                    | | Cobán, GT             |  | Guatemala, GT         |        |
|                    | | lluvia 22 C - 14:32   |  | despejado 25 C - ayer |        |
|                    | | [Ver clima ahora]     |  | [Ver clima ahora]     |        |
|                    | +-----------------------+  +-----------------------+        |
|                    |                                                             |
|                    | Alertas recientes                          [Ver todas]      |
|                    | | ALTA  Cobán: 33.4 C supera tu umbral de 32 C  (NUEVA)     |
|                    | | LLUVIA Lluvia detectada en Cobán - ESP32      (NUEVA)     |
+--------------------+-------------------------------------------------------------+
```

### 4. Historial (M08)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]        (!)2  Ana v                   |
+----------------------------------------------------------------------------------+
|   Inicio           | Historial de consultas                                      |
|   Favoritos        |                                                             |
| > Historial        | Tipo [Todos v]  Ubicación [Todas v]                         |
|   Alertas          | Desde [__/__/____]  Hasta [__/__/____]   [Filtrar]          |
|   Config. alertas  |                                                             |
|   Mis dispositivos | Fecha       Ubicación  Tipo        Temp  Condición   Hum    |
|                    | ----------- ---------- ----------- ----- ----------- ----   |
|                    | 07/10 14:32 Cobán      (ACTUAL)    22 C  lluvia lig. 88 %   |
|                    | 07/10 14:33 Cobán      (PRONOSTICO)  -   -             -    |
|                    | 06/10 09:10 Guatemala  (ACTUAL)    25 C  cielo claro 60 %   |
|                    |                                                             |
|                    |         [Consultar de nuevo] en cada fila      < 1 2 3 >    |
+--------------------+-------------------------------------------------------------+
```

### 5. Alertas (M09)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]        (!)2  Ana v                   |
+----------------------------------------------------------------------------------+
|   Inicio           | Mis alertas                   [ Configurar alertas ]        |
|   Favoritos        |                                                             |
|   Historial        | Estado: [Todas] [Nuevas] [Leídas]      Tipo [Todos v]       |
| > Alertas          |                                                             |
|   Config. alertas  | # [ALTA] Cobán: 33.4 C supera tu umbral de 32 C             |
|   Mis dispositivos | # Cobán, GT - 07/10 14:32  (OpenWeather) (NUEVA)            |
|                    | #                          [ Marcar como leída ]            |
|                    | ----------------------------------------------------        |
|                    | # [LLUVIA] Lluvia detectada en Cobán                        |
|                    | # Cobán, GT - 07/10 13:05  (ESP32) (NUEVA)                  |
|                    | #                          [ Marcar como leída ]            |
|                    | ----------------------------------------------------        |
|                    | # [BAJA] Guatemala: 9.5 C bajo tu umbral de 10 C            |
|                    | # Guatemala, GT - 05/10 06:00 (OpenWeather) (LEIDA)         |
|                    |                                                             |
|                    |                                             < 1 2 >         |
+--------------------+-------------------------------------------------------------+
  # = borde izquierdo de color según el tipo (naranja / cian / índigo)
```

### 6. Mis dispositivos (M11)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]        (!)2  Ana v                   |
+----------------------------------------------------------------------------------+
|   Inicio           | Mis dispositivos         [ + Registrar dispositivo ]        |
|   Favoritos        |                                                             |
|   Historial        | +------------------------+  +------------------------+      |
|   Alertas          | | Patio casa             |  | Laboratorio            |      |
|   Config. alertas  | | Cobán, GT              |  | Cobán, GT              |      |
| > Mis dispositivos | | (Activo)  o En línea   |  | (Inactivo) o Fuera de  |      |
|                    | | Última conexión: 3 min |  |            línea       |      |
|                    | | 21.8 C 85% Lluvia 40%  |  | Última conexión: 2 días|      |
|                    | | [Ver detalle] [Editar] |  | [Ver detalle] [Activar]|      |
|                    | | [Desactivar]           |  |                        |      |
|                    | +------------------------+  +------------------------+      |
|                    |                                                             |
|                    | Modal que aparece al registrar:                             |
|                    | +--------------------------------------------+              |
|                    | | Tu API Key (se muestra una sola vez)       |              |
|                    | | [ a8F3k..................Zq91 ] [Copiar]  |               |
|                    | | ! Guárdala ahora; no podrás verla de nuevo |              |
|                    | |                               [Entendido]  |              |
|                    | +--------------------------------------------+              |
+--------------------+-------------------------------------------------------------+
```

### 7. Dashboard administrador (M13)

```
+----------------------------------------------------------------------------------+
| ClimaPlus      [ Buscar ubicación...      ]        (!)0  Admin v                 |
+----------------------------------------------------------------------------------+
| ADMINISTRACIÓN     | Panel administrador   Periodo [Hoy] [7 días] [30 días]      |
| > Panel            |                                                             |
|   Usuarios         | +----------+ +----------+ +----------+ +----------+         |
|   Alertas sistema  | | 38 / 41  | | 126      | | 17       | | 5        |         |
|                    | | usuarios | | consultas| | alertas  | | disposit.|         |
| MI CUENTA          | | activos  | | 98A  28P | | A9 B3 L5 | | activos  |         |
|   Inicio           | +----------+ +----------+ +----------+ +----------+         |
|   Favoritos        |                                                             |
|   ...              | Ubicaciónes más consultadas  Alertas recientes              |
|                    | +-------------------------+ +-------------------------+     |
|                    | | 1. Cobán            54  | | 14:32 Ana  ALTA   Cobán |     |
|                    | | 2. Guatemala        31  | | 13:05 Ana  LLUVIA Cobán |     |
|                    | | 3. Quetzaltenango   12  | | 11:20 Luis BAJA   Xela  |     |
|                    | | 4. Antigua           9  | |             [Ver todas] |     |
|                    | | 5. Flores            6  | +-------------------------+     |
|                    | +-------------------------+                                 |
+--------------------+-------------------------------------------------------------+
  98A 28P = consultas ACTUAL / PRONOSTICO   A9 B3 L5 = alta / baja / lluvia
```

### Referencia móvil (390 px) – M04

```
+------------------------------+
| =  ClimaPlus           (!)2  |
+------------------------------+
| [ Buscar ubicación...      ] |
| +--------------------------+ |
| | Cobán - GT          [*]  | |
| |   [lluvia]  22 C         | |
| |   lluvia ligera          | |
| | 23 C - 88 % - 2.1 m/s    | |
| +--------------------------+ |
| [ Ver pronóstico de 5 días ] |
| < Lun | Mar | Mié | Jue >    |
+------------------------------+
  (las ForecastCards se desplazan horizontalmente)
```

---

## Pendientes de esta especificación

- [ ] Crear el archivo en Figma siguiendo la Parte A.
- [ ] Diseñar M01–M15 (escritorio) y las versiones móviles indicadas.
- [ ] Agregar el enlace de Figma al inicio de este documento.
- [ ] Exportar capturas PNG a `docs/mockups/`.
