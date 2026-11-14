# ESP32 DevKit con LCD: auditoría del firmware y plan de integración (Fase 7)

Montaje oficial para la entrega final (confirmado el 11-nov-2026): **ESP32 DevKit, DHT11, LDR, sensor de lluvia,
LCD 16x2 I2C, buzzer, un botón y tres LED (rojo, verde y amarillo)**. Firmware:
`hardware/esp32/climaplus_esp32_lcd/climaplus_esp32_lcd.ino`. Conexiones y puesta en marcha:
[`hardware/esp32/README.md`](../../hardware/esp32/README.md). Contrato:
[`../api/contrato-dispositivos-lecturas.md`](../api/contrato-dispositivos-lecturas.md).

La versión BME280 + OLED (`hardware/esp32/climaplus_esp32`) y la simulación de Wokwi quedan como historial y no
forman parte del montaje oficial.

## 1. Comunicación y autenticación

| Tema | Cómo funciona |
|---|---|
| Protocolo | HTTP/1.1 + JSON por WiFi 2.4 GHz (`HTTPClient` del ESP32), en la red local |
| Endpoint | `POST /api/lecturas` (Docker: puerto 8080; `dotnet run`: 5208 con `--urls http://0.0.0.0:5208`) |
| Autenticación | Cabecera `X-Device-Key: cpk_…` (no usa JWT). La API guarda solo el SHA-256 de la clave y la compara antes de leer el cuerpo |
| Cuerpo | `{ "temperatura": 24.50, "humedad": 68.00, "lluvia": false, "luz": 72 }`; cada campo es opcional, debe venir al menos uno |
| Respuesta | 201 con `alertasGeneradas`; 400, 401, 403 y 429 (con `Retry-After`) |
| Frecuencia | Una lectura cada 10 s; límite de la API: 30 por minuto por dispositivo |
| "En línea" | El dispositivo aparece en línea si la última lectura llegó hace 5 minutos o menos |
| Alertas | Usa las reglas del dueño para la ubicación del dispositivo; una alerta del mismo tipo no se repite durante 60 minutos para esa ubicación |

## 2. Auditoría del firmware (solo lectura; el firmware no se modificó)

| # | Revisión | Resultado |
|---|---|---|
| A-1 | Pines | Correctos: la LDR en ADC1 (GPIO34), sin pines de arranque (0, 2, 12, 15), I2C en 21/22 |
| A-2 | Sensores | DHT11 en GPIO23 con validación de rango y `NaN`; lluvia digital con `INPUT_PULLUP` y lógica configurable; LDR normalizada a 0–100 % |
| A-3 | Contrato | El JSON coincide con `LecturaRequest`: 2 decimales en temperatura y humedad, luz entera, sin `fechaLectura` |
| A-4 | Respuestas | Trata 201 (con y sin alerta), 400, 401, 403 y 429 con `Retry-After`; cualquier otro código muestra "Error de la API" |
| A-5 | Secretos | WiFi y API Key solo en `secrets.h` (ignorado por Git); la clave no se imprime por el monitor serie |
| A-6 | Tiempo | Comparaciones con `millis()` seguras ante el desbordamiento (cada ~49 días) |
| A-7 | LCD | Detecta la dirección 0x27 o 0x3F; si no hay LCD, el resto sigue funcionando |
| A-8 | Bloqueo durante el envío | ⚠️ Cada POST puede bloquear el `loop` hasta 8 s (`TIMEOUT_HTTP_MS`) si la API no responde: mientras tanto el botón y el buzzer no reaccionan. Aceptable para la entrega; no requiere cambios |
| A-9 | Voltaje del LCD | ⚠️ Por verificar en el montaje: si el módulo I2C tiene pull-up a 5 V, SDA/SCL llegan a 5 V al ESP32 (ver README, sección 2) |
| A-10 | Texto en el LCD | El mensaje de alerta se recorta a 32 caracteres y se le quitan tildes y ñ; los mensajes de la API usan "C" en lugar de "°C" |
| A-11 | Red | HTTP sin cifrar dentro de la red local; HTTPS queda pendiente para un servidor público |
| A-12 | Comentario de `secrets.example.h` | Sugiere el puerto 5208 "en modo desarrollo": requiere iniciar la API con `--urls http://0.0.0.0:5208` (documentado en el README) |

Conclusión: el firmware cumple el contrato y no necesita cambios para la entrega. Los puntos ⚠️ son de montaje o de
despliegue, no de código.

## 3. Preparación

1. API accesible desde la red local (README, sección 4.4) y regla del firewall.
2. Usuario con una ubicación favorita y una regla de alerta de temperatura alta (o de lluvia).
3. Dispositivo registrado en **Mis dispositivos**; API Key copiada en `secrets.h`.
4. Monitor serie a 115200 baudios.

## 4. Pruebas del dispositivo físico

Registra en cada fila la fecha, el resultado y una foto o captura del monitor serie.

| ID | Prueba | Pasos | Resultado esperado |
|---|---|---|---|
| H-01 | Arranque | Alimentar el ESP32 | Los 3 LED se encienden uno por uno; el LCD muestra "ClimaPlus ESP32" y luego "WiFi conectado" con la IP |
| H-02 | LCD | Observar 10 s | Alterna mediciones y estado cada 3 s |
| H-03 | DHT11 | Soplar aire tibio o acercar la mano | Temperatura y humedad cambian en el LCD y en M12; nunca `--` de forma permanente |
| H-04 | DHT11 desconectado | Retirar DATA unos segundos | El monitor serie dice "DHT sin lectura"; la lectura se envía igual (201) con lluvia y luz |
| H-05 | LDR | Tapar y destapar | La luz baja cerca de 0 % y sube cerca de 100 % (si no, calibrar con `prueba_ldr`) |
| H-06 | Lluvia | Mojar la placa sensora | "Lluvia:Si" en el LCD y `lluvia = true` en M12; al secar vuelve a "No" |
| H-07 | Envío normal | Esperar 30 s | 3 lecturas con 201; LED verde; el dispositivo aparece **En línea** |
| H-08 | Alerta física | Regla con umbral menor que la temperatura ambiente | LED rojo, 3 pitidos, mensaje en el LCD; la alerta aparece en M09 y M12 con origen ESP32 |
| H-09 | Anti-duplicado | Seguir enviando | No se repite la alerta del mismo tipo durante 60 min |
| H-10 | Botón corto | Pulsar durante los pitidos | El buzzer se silencia |
| H-11 | Botón largo | Mantener 2 s | Se envía una lectura en ese momento |
| H-12 | API Key inválida | Cambiar un carácter de `DEVICE_KEY` y subir | "API Key invalida", LED amarillo, HTTP 401; la consola de la API registra `dispositivo_rechazado` sin mostrar la clave |
| H-13 | Dispositivo desactivado | Desactivarlo en **Mis dispositivos** | "Desactivado", LED amarillo, HTTP 403; al reactivarlo vuelve a 201 |
| H-14 | Sin WiFi | Apagar el router o cambiar la red | "Sin WiFi", LED amarillo; reconecta solo al volver la red |
| H-15 | API detenida | Detener la API | "Sin respuesta" tras el tiempo de espera; al iniciarla de nuevo vuelve a 201 |
| H-16 | Fuera de línea | Apagar el ESP32 más de 5 min | El dispositivo aparece fuera de línea en M11 |
| H-17 | Alimentación externa | Cargador USB de 5 V sin la PC | Funciona igual que con la PC |

**Sensores pendientes.** Para los dos sensores nuevos, repite su prueba (H-03/H-04 para el DHT11, H-05 para la
LDR, H-06 para el de lluvia) y anota el valor crudo del monitor serie antes de montarlos en la caja.

## 5. Criterios de aceptación

- H-01 a H-17 aprobadas, con evidencia.
- Ninguna lectura rechazada con 400 durante 10 minutos de funcionamiento continuo.
- Las alertas del ESP32 se ven en el frontend y en el LCD.
- La API Key no aparece en el monitor serie, en la consola de la API ni en Git.

## 6. Pendientes fuera del alcance de la entrega local

- HTTPS para un servidor público (proxy con certificado y `WiFiClientSecure` validando la CA en el ESP32).
- Configuración WiFi sin recompilar (por ejemplo, un portal de configuración); hoy se cambia en `secrets.h`.
