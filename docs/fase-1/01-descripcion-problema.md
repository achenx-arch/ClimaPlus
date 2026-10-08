# Descripción del problema

**Proyecto:** ClimaPlus – Panel de clima y alertas ambientales
**Curso:** Desarrollo Web · Universidad Mariano Gálvez de Guatemala
**Fase:** 1 – Planeación y diseño inicial

---

## Contexto

El clima influye en muchas decisiones cotidianas: salir o no con paraguas, planificar un viaje, programar una actividad al aire libre o simplemente saber si conviene abrigarse. Hoy esa información se obtiene principalmente de aplicaciones móviles genéricas o de sitios web meteorológicos, que muestran el clima actual y el pronóstico de cualquier ciudad.

Además del clima "oficial" que publican estos servicios, cada vez es más accesible medir las condiciones del entorno inmediato con hardware de bajo costo, como una placa ESP32 con sensores de temperatura, humedad, lluvia y luz.

## Problema identificado

Las herramientas meteorológicas de uso común resuelven bien la consulta puntual, pero tienen limitaciones cuando una persona quiere hacer un seguimiento más personal:

- **No hay un registro propio.** El usuario consulta el clima, pero no queda un historial de lo que consultó, cuándo y qué condiciones había en ese momento.
- **Las alertas no son personalizadas.** Los avisos suelen ser generales y no permiten definir umbrales propios, como "avísame si en mi ciudad pasa de 32 °C".
- **Las ubicaciones de interés se repiten.** Hay que volver a buscar las mismas ciudades una y otra vez.
- **Los datos locales quedan aislados.** Las mediciones de un sensor propio no se integran con la información meteorológica general ni generan alertas en el mismo lugar.
- **No hay una vista de administración.** En un servicio con varios usuarios no existe una forma sencilla de conocer cuántas consultas y alertas se generan.

## Necesidad

Se necesita una aplicación web que reúna en un solo lugar:

1. La consulta del clima actual y del pronóstico de cualquier ubicación, accesible incluso sin registrarse.
2. Un espacio personal donde el usuario autenticado pueda guardar ubicaciones favoritas, revisar su historial de consultas y recibir alertas según sus propios umbrales.
3. La posibilidad de conectar un dispositivo ESP32 que envíe mediciones físicas y que esas mediciones también puedan generar alertas.
4. Un rol administrador que gestione usuarios y consulte alertas y estadísticas básicas.

Todo esto debe hacerse de forma segura: las credenciales, la clave de la API meteorológica y las claves de los dispositivos no deben quedar expuestas.

## Solución propuesta

**ClimaPlus** es una aplicación web con arquitectura cliente-servidor:

- Un **frontend** en React + TypeScript + Bootstrap, donde el usuario busca ubicaciones, consulta el clima y gestiona su información.
- Un **backend** en ASP.NET Core Web API, que centraliza toda la lógica. Consulta OpenWeather, valida y normaliza los datos, evalúa las alertas y controla el acceso por roles.
- Una base de datos **PostgreSQL**, a la que se accede con Entity Framework Core. Almacena usuarios, ubicaciones, favoritos, historial, reglas de alerta, alertas, dispositivos y lecturas.
- En una fase posterior, un **ESP32** con sensores que envía lecturas al backend mediante HTTP/JSON, autenticado con una API Key.

El frontend nunca consulta OpenWeather ni la base de datos directamente. El ESP32 tampoco accede a la base de datos. Todo pasa por el backend.

## Justificación

- **Valor práctico:** combina información meteorológica general con preferencias personales (favoritos, umbrales) y con mediciones del entorno real, algo que las aplicaciones genéricas no ofrecen juntas.
- **Valor académico:** el proyecto integra los contenidos del curso de Desarrollo Web: frontend moderno con componentes, API REST, autenticación con JWT, autorización por roles, consumo de una API externa, persistencia con un ORM y diseño de una base de datos relacional normalizada.
- **Alcance realizable:** se usan tecnologías conocidas y una API meteorológica con plan gratuito. El modelo se limita a 8 tablas, y la integración con hardware se deja para una fase posterior, de modo que el núcleo web pueda terminarse primero.
- **Buenas prácticas:** el diseño aplica desde el inicio separación por capas, contraseñas almacenadas como hash, secretos solo en el servidor, validación del lado servidor y control de versiones con Git.
