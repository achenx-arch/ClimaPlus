# Diagnóstico de los sensores pendientes: lluvia MH-RD con relé y LDR (Fase 7)

Estado: **diagnóstico preparado, sin pruebas físicas todavía.** Nada de lo que dice "propuesto" está validado; las
conexiones definitivas del sensor de lluvia dependen de confirmar el pinout real del módulo (sección 3.3).

- Firmware principal (no modificado): `hardware/esp32/climaplus_esp32_lcd/climaplus_esp32_lcd.ino`.
- Programas de diagnóstico nuevos, independientes del firmware:
  `hardware/esp32/diagnostico_lluvia_mhrd/diagnostico_lluvia_mhrd.ino` y
  `hardware/esp32/diagnostico_ldr/diagnostico_ldr.ino`. La prueba anterior `prueba_ldr` se conserva.
- Verificación de compilación (12-nov-2026, `arduino-cli` 1.1.1, núcleo esp32 3.0.7, placa
  `esp32doit-devkit-v1`, librerías LiquidCrystal I2C 1.1.4, DHT sensor library 1.4.6, Adafruit Unified Sensor
  1.1.14 y ArduinoJson 7.2.1): el firmware principal (82 % del flash), `prueba_ldr` y los dos diagnósticos compilan
  sin errores; los diagnósticos, sin advertencias. Compilar no prueba que el hardware funcione.

## 1. Inventario definitivo del hardware

| # | Componente | Estado según el responsable | Evidencia en este documento |
|---|---|---|---|
| 1 | ESP32 DevKit ("DOIT ESP32 DEVKIT V1") | Funciona | — |
| 2 | DHT11 (temperatura y humedad) | Funciona | Pendiente registrar H-03/H-04 |
| 3 | LCD 16x2 con módulo I2C (PCF8574) | Funciona | Pendiente medir voltaje de SDA/SCL (A-9) |
| 4 | Buzzer (con transistor NPN según el firmware) | Funciona | — |
| 5 | Botón | Funciona | — |
| 6 | LED rojo, verde y amarillo | Funcionan | — |
| 7 | LDR | **Falló en pruebas anteriores** | Sección 4 |
| 8 | Sensor de lluvia **MH-RD + módulo de relé** (NO/COM/NC, potenciómetro, LED) | **Nuevo, sin validar** | Sección 3 |

No forman parte del montaje: BME280, OLED, LED RGB (versión anterior) ni la placa LM393 con salida DO que asumía el
README; el README se actualizará cuando el MH-RD quede confirmado.

## 2. GPIO y conexiones actuales (según el firmware)

| Componente | GPIO | Tipo de conexión | Voltaje | Configuración en el código | Posibles conflictos | Validación |
|---|---|---|---|---|---|---|
| LCD SDA / SCL | 21 / 22 | I2C (`Wire.begin(21, 22)`) | Módulo normalmente a 5 V; bus del ESP32 a 3,3 V | Dirección 0x27 o 0x3F autodetectada | Pull-up del módulo a 5 V (A-9): medir | Funciona (usuario) |
| DHT11 DATA | 23 | Digital, 1 hilo | 3,3–5,5 V; alimentar a 3V3 | `DHT dht(23, DHT11)`, `dht.begin()`; `NaN` y rango validados | Ninguno | Funciona (usuario) |
| Lluvia | 27 | Digital | Entrada de 3,3 V | `pinMode(27, INPUT_PULLUP)`; `LLUVIA_ACTIVA_EN_BAJO = true` (LOW = lluvia) | GPIO27 es ADC2/touch, pero aquí se usa solo como digital: sin conflicto con WiFi | **Pendiente (MH-RD)** |
| LDR | 34 | Analógica (divisor) | 0–3,3 V máximo | `analogRead(34)`, `analogReadResolution(12)`, atenuación por defecto del núcleo 3.x (11 dB); `map(crudo, 50, 800, 0, 100)` | ADC1: sin conflicto con WiFi. GPIO34 no tiene pull-up/pull-down: flota si se suelta | **Pendiente** |
| LED rojo | 25 | Salida, 220 Ω | 3,3 V | `OUTPUT` | Ninguno (DAC1/ADC2 sin usar) | Funciona |
| LED verde | 26 | Salida, 220 Ω | 3,3 V | `OUTPUT` | Ninguno (DAC2/ADC2 sin usar) | Funciona |
| LED amarillo | 33 | Salida, 220 Ω | 3,3 V | `OUTPUT` | Ninguno | Funciona |
| Buzzer | 32 | Salida a la base de un NPN | 3,3 V en el GPIO | `OUTPUT`, HIGH = suena (buzzer activo) | Ninguno | Funciona |
| Botón | 4 | Digital a GND | 3,3 V | `INPUT_PULLUP`; corta = silenciar, 2 s = enviar | GPIO4 no es pin de arranque | Funciona |

Revisión general del firmware:

| Tema | Resultado |
|---|---|
| Inicialización | LED, buzzer, lluvia y botón con `pinMode`; I2C antes del LCD; `dht.begin()` antes del WiFi |
| ADC | 12 bits; no fija la atenuación (usa la de fábrica del núcleo: 11 dB, ~0,15–3,1 V útiles); `prueba_ldr` sí la fija |
| WiFi | Pines analógicos solo en ADC1 (GPIO34): correcto. Reintenta la conexión cada 10 s |
| Lecturas inválidas | DHT: `NaN` o fuera de −40…85 °C se omite. Lluvia y luz siempre se envían: **un pin flotante o mal calibrado no se detecta en el firmware** |
| Muestreo | Una lectura cada 10 s (API: máximo 30/min). La lluvia se lee una sola vez por envío, sin antirrebote |
| Envío | `POST /api/lecturas`, `Content-Type: application/json`, `X-Device-Key`; 201/400/401/403/429 tratados; `alertasGeneradas` → LED rojo, buzzer y LCD |
| Compatibilidad con la API | El JSON coincide con `LecturaRequest`: `temperatura` y `humedad` con 2 decimales, `lluvia` booleano, `luz` entero 0–100 |

## 3. Sensor de lluvia MH-RD con módulo de relé

### 3.1 Cómo funciona esta variante

La placa MH-RD es solo una superficie con pistas entrelazadas: el agua baja la resistencia entre ellas (por eso se
conecta con dos hilos). El módulo controlador compara esa resistencia con el umbral del potenciómetro (en los módulos
de este tipo, con un comparador como el LM393) y, según el resultado, **activa o desactiva el relé**. Los terminales
`NO`, `COM` y `NC` son los contactos del relé:

- Relé en reposo: `COM` conectado con `NC`; `NO` abierto.
- Relé activado: `COM` conectado con `NO`; `NC` abierto.

Puntos que **no** se pueden suponer y deben comprobarse con el módulo en la mano:

| Duda | Por qué importa | Cómo se confirma |
|---|---|---|
| Voltaje de alimentación | Estos módulos se venden en 5 V, 9 V y 12 V; el relé solo conmuta con su voltaje | Leer la inscripción del relé azul: `SRD-05VDC-SL-C` = 5 V; `SRD-12VDC-SL-C` = 12 V |
| Pines de alimentación y señal | Algunos traen solo `VCC/GND` (o `DC+/DC-`); otros además `DO`/`AO` o `IN` | Leer la serigrafía junto a cada pin y fotografiar ambas caras de la placa |
| Si el relé se activa con agua o en seco | Algunos vendedores indican que el relé se **apaga** al detectar agua | Prueba L-4 con multímetro |
| Si los contactos están aislados del circuito | Solo así se pueden leer con el ESP32 sin riesgo | Prueba L-2 (continuidad contra `VCC` y `GND`) |

### 3.2 Respuestas a las preguntas del diagnóstico

| # | Pregunta | Respuesta |
|---|---|---|
| 1 | Funcionamiento | Placa resistiva → comparador con umbral (potenciómetro) → transistor → bobina del relé → contactos `NO/COM/NC` |
| 2 | Alimentación | La del relé (5 V o 12 V según su inscripción). Un módulo de 5 V puede tomar 5 V del pin `VIN` del ESP32 cuando está alimentado por USB; uno de 12 V necesita un adaptador de 12 V aparte. Nunca de un GPIO ni del pin 3V3 |
| 3 | Pines para el estado | Los **contactos del relé** (`COM` + `NO` o `NC`) siempre; un `DO` solo si existe en la serigrafía |
| 4 | Salida digital compatible | Los contactos del relé son un interruptor sin voltaje propio ("contacto seco"): con `COM` a GND y el contacto a un GPIO con `INPUT_PULLUP`, el GPIO solo ve 0 V o 3,3 V |
| 5 | Interfaz de protección | Con los contactos del relé no hace falta: el relé ya aísla. Un `DO` de un módulo de 5 V necesitaría un divisor (por ejemplo 10 kΩ en serie y 20 kΩ a GND → ~3,3 V); con 12 V, no usarlo (usar el relé). No hace falta optoacoplador |
| 6 | Contactos secos con pull-up | Sí, es la opción recomendada. Pull-up interno de GPIO27 (~45 kΩ); si la lectura parpadea, una resistencia externa de 4,7 kΩ a 3V3 da más corriente al contacto |
| 7 | Probar sin el ESP32 | Pruebas L-1 a L-5: inscripciones, continuidad en reposo, alimentación sola, seco/mojado con el multímetro |
| 8 | Ajustar el potenciómetro | Con la placa seca, girar hasta el punto en que el LED de salida cambia y regresar un poco para que en seco quede estable; luego comprobar con gotas (L-5) |
| 9 | Seco o mojado | LED de salida del módulo, el "clic" del relé y la continuidad `COM–NO` / `COM–NC` (tabla de L-4) |
| 10 | Integración | Usar el **mismo GPIO27** y el contacto que se **cierra con agua**: así el firmware actual (`INPUT_PULLUP`, LOW = lluvia) funciona sin cambios de código (sección 6) |

Consideraciones:
- **Falla segura:** si el contacto que se cierra con agua es `NC` (relé activado en seco), al quedarse el módulo sin
  alimentación el ESP32 leería "lluvia". Revisa que el LED de encendido del módulo esté prendido durante la demostración.
- Los relés de 10 A no están pensados para corrientes muy pequeñas: si el contacto da lecturas intermitentes, usa la
  pull-up externa de 4,7 kΩ.
- La placa MH-RD se corroe si queda mojada y alimentada mucho tiempo; sécala después de cada prueba.

### 3.3 Información que necesito para dar el cableado definitivo

1. Inscripción completa del relé azul (por ejemplo `SRD-05VDC-SL-C`).
2. Foto de ambas caras del módulo controlador y nombres impresos junto a cada pin o borne (cuántos son).
3. Resultado de las pruebas L-2 y L-4 (tabla de continuidad).

## 4. Diagnóstico de la LDR

| # | Pregunta | Respuesta |
|---|---|---|
| 1 | Tipo de LDR | El repositorio supone una fotorresistencia suelta (GL5528 en el README anterior) en divisor con una resistencia fija. **Falta confirmar físicamente** |
| 2 | Suelta o módulo | Suelta: 2 patas, sin placa. Módulo: placa con `VCC`, `GND`, `DO` y/o `AO` y un potenciómetro. Indícame cuál tienes |
| 3 | GPIO | 34 |
| 4 | Analógica o digital | Analógica (`analogRead`) |
| 5 | Divisor | Sí, si es suelta: `3V3 → LDR → D34 → R → GND`. Si es módulo, ya lo trae (usar `AO` con el módulo a 3V3) |
| 6 | ADC1 o ADC2 | ADC1 (canal 6) |
| 7 | Conflictos | Ninguno con WiFi (los de ADC2 sí fallarían). GPIO34 es solo entrada y sin pull-up/pull-down: si un cable se suelta, lee valores aleatorios |
| 8 | Calibración | Ver 4.2 |
| 9 | Luz y oscuridad | Prueba D-1 a D-5 con `diagnostico_ldr` |
| 10 | Llega a la API | Prueba D-6 |

### 4.1 Posibles causas de los fallos anteriores

| # | Causa | Cómo se ve | Cómo se detecta |
|---|---|---|---|
| 1 | **Calibración que no corresponde al circuito.** El firmware usa `LDR_OSCURO = 50` y `LDR_LUZ = 800`; `prueba_ldr` calcula con 200 y 3800 y dice "misma fórmula del firmware" (ya no lo es). Con 10 kΩ, una LDR GL5528 con luz de interior da aproximadamente 1,5–2,5 V (ADC ~1700–3000, según la luz): todo lo que pase de 800 se envía como 100 % | Luz casi siempre 100 % salvo con la LDR tapada | `diagnostico_ldr` muestra `ADC` y `luz_firmware` lado a lado |
| 2 | Resistencia del divisor equivocada (1 kΩ café-negro-rojo en lugar de 10 kΩ café-negro-naranja) | Rango muy corto o desplazado | Colores de la resistencia; la `R_FIJA` del diagnóstico debe coincidir |
| 3 | Divisor invertido | Más luz = menos porcentaje | Al iluminar, el ADC baja en lugar de subir |
| 4 | Alimentada con 5 V/VIN | ADC pegado en 4095 (y riesgo para el pin) | Estado `SATURADO` |
| 5 | Cable flojo o pin flotante | Valores que saltan sin cambiar la luz | Estado `RUIDO ALTO` |
| 6 | Conectada antes a un pin ADC2 | Lecturas en 0 o fallidas con el WiFi encendido | Comando `w` del diagnóstico (en D34 no debe cambiar) |
| 7 | LDR tapada dentro de la caja | Siempre oscuro | Probar fuera de la caja y dentro |
| 8 | Zona no lineal del ADC (< ~150 mV o > ~3,1 V) | 0 o 100 % en los extremos | `V` en mV del diagnóstico |

### 4.2 Calibración

1. Carga `diagnostico_ldr` con `R_FIJA` y `LDR_ARRIBA` acordes a tu circuito.
2. Escribe `r`, tapa la LDR por completo 5 s y luego ilumínala directamente con la linterna del teléfono 5 s.
3. Escribe `c`: el programa sugiere `LDR_OSCURO` y `LDR_LUZ` (mínimo y máximo con un margen del 5 %).
4. Con autorización, esos dos valores se copian al firmware principal (sección 6).

## 5. Diagrama de conexiones propuesto (sujeto a confirmación física)

```
LDR suelta (divisor de tensión, solo 3,3 V):

  ESP32 3V3 ──── LDR ────┬──── ESP32 D34 (ADC1)
                         └──── R_FIJA 10 kΩ ──── ESP32 GND

Sensor de lluvia MH-RD (contactos secos del relé):

  ESP32 GND ──────────── COM  ┐
  ESP32 D27 (pull-up) ── NO o NC (*) ┘ relé del módulo
  Módulo VCC/GND (o DC+/DC-) ── fuente con el voltaje del relé (5 V: VIN del ESP32 con USB; 12 V: adaptador aparte)
  Placa MH-RD ──(2 hilos)── entrada de la placa sensora del módulo
  Ningún otro pin del módulo va al ESP32.

  (*) El contacto que se CIERRA cuando la placa está mojada (se determina en la prueba L-4).
```

Si el módulo de 5 V se alimenta desde `VIN`, su `GND` es el mismo del ESP32 (sin problema). Con una fuente de 12 V
aparte, no hace falta unir las tierras: solo `COM` va al GND del ESP32.

## 6. Procedimiento paso a paso

### Sensor de lluvia (sin ESP32: L-1 a L-5; con ESP32: L-6 a L-8)

| Paso | Acción | Resultado esperado / qué anotar |
|---|---|---|
| L-1 | Desconectado: leer la inscripción del relé y la serigrafía de los pines; foto de ambas caras | Voltaje del módulo y nombres de los pines |
| L-2 | Desconectado, multímetro en continuidad: `COM–NC`, `COM–NO`, y cada borne (`COM`, `NO`, `NC`) contra `VCC` y contra `GND` | `COM–NC` cerrado, `COM–NO` abierto; **ningún borne del relé con continuidad a VCC ni a GND** (si la hay, no conectar al ESP32 y avisar) |
| L-3 | Alimentar el módulo con su voltaje (sin ESP32). Placa MH-RD conectada y seca | LED de encendido prendido |
| L-4 | Con el módulo alimentado, medir continuidad `COM–NO` y `COM–NC` con la placa **seca** y luego con **unas gotas** de agua sobre las pistas | Tabla: seco → (`COM–NO` ?, `COM–NC` ?), mojado → (?, ?), LED de salida y clic del relé. El contacto que se cierra **mojado** es el que irá a D27 |
| L-5 | Ajustar el potenciómetro (con la placa seca y luego con gotas) y secar con papel | Seco estable, mojado cambia en 1–2 s, vuelve al secar |
| L-6 | Sin alimentar nada: `COM` → GND del ESP32; contacto elegido → D27. Retirar cualquier cable anterior de D27. Cargar `diagnostico_lluvia_mhrd` | — |
| L-7 | Alimentar el módulo y el ESP32; monitor serie a 115200; mojar y secar 3 veces | Seco = `ABIERTO (D27=HIGH)`, LED verde; mojado = `CERRADO (D27=LOW)`, LED rojo; sin `AVISO inestable` |
| L-8 | Desconectar la alimentación del módulo con la placa seca | Anotar qué lee D27 (falla segura, sección 3.2) |

### LDR (D-1 a D-6)

| Paso | Acción | Resultado esperado |
|---|---|---|
| D-1 | Sin alimentación: identificar si es LDR suelta o módulo, el valor de la resistencia fija (colores) y medir la LDR con el ohmímetro tapada y con luz | Tapada: cientos de kΩ o más; con luz: pocos kΩ |
| D-2 | Revisar el cableado: un extremo de la LDR a **3V3** (no a 5 V/VIN), el otro a D34; la resistencia de D34 a GND | — |
| D-3 | Cargar `diagnostico_ldr` (ajustar `R_FIJA` y `LDR_ARRIBA`), monitor a 115200 | Estado `OK`, desviación pequeña (±20 o menos) |
| D-4 | Tapar 5 s, iluminar 5 s, luz ambiente | El ADC sube con la luz (con `LDR_ARRIBA = true`); la resistencia estimada coincide con D-1 |
| D-5 | Escribir `w` (WiFi encendido) y repetir D-4; luego `c` | Mismas lecturas con y sin WiFi; anotar la sugerencia de calibración |
| D-6 | Con el firmware principal (después de la autorización), comparar el `luz=%` del monitor serie con el detalle del dispositivo (M12) | Mismo valor en el monitor, en M12 y en `lectura_sensor.luz` |

Consulta de solo lectura para confirmar el almacenamiento (en la base que esté usando la API):

```sql
SELECT id_lectura, fecha_lectura, temperatura, humedad, lluvia, luz
FROM lectura_sensor
WHERE id_dispositivo = <id> ORDER BY id_lectura DESC LIMIT 10;
```

## 7. Programas mínimos de prueba

| Programa | Qué hace | Pines |
|---|---|---|
| `diagnostico_lluvia_mhrd` | Lee el contacto del relé con antirrebote; muestra cambios, duración y avisa si es inestable; LED rojo = cerrado, verde = abierto; buzzer apagado | D27 (entrada), D25/D26/D33 (LED), D32 (buzzer en LOW) |
| `diagnostico_ldr` | Promedio de 32 muestras, desviación, mV, resistencia estimada, `luz` con la fórmula actual del firmware, detección de flotante/saturado, WiFi on/off, sugerencia de calibración | D34 |

Ambos usan los mismos GPIO del firmware: no hay que mover cables entre la prueba y la integración.

## 8. Plan de integración con el firmware (requiere autorización)

| Caso | Cambio en `climaplus_esp32_lcd.ino` |
|---|---|
| El contacto que se cierra mojado va a D27 | **Ninguno**: `INPUT_PULLUP` + `LLUVIA_ACTIVA_EN_BAJO = true` ya interpretan LOW como lluvia |
| Solo es posible usar el contacto que se cierra en **seco** | Cambiar una constante: `LLUVIA_ACTIVA_EN_BAJO = false` |
| La lectura del relé rebota (`AVISO inestable`) aunque se ajuste el potenciómetro | Opcional: leer el pin 5 veces con 10 ms de separación y tomar la mayoría (cambio pequeño en `leerSensores`) |
| LDR | Reemplazar `LDR_OSCURO` y `LDR_LUZ` por los valores de D-5 |
| Documentación | README (fila del sensor de lluvia y alimentación del módulo), comentario de `prueba_ldr` sobre la fórmula, plan de integración |

No se agregan funciones nuevas, no cambian los GPIO, ni el contrato de la API, ni la base de datos.

Verificación después de integrar: monitor serie (`Sensores: T=… H=… lluvia=… luz=…` y `POST /api/lecturas -> 201`),
M11 "En línea", M12 con lluvia y luz, `lectura_sensor` (consulta de arriba), una regla de **lluvia** que genere la
alerta LLUVIA (LED rojo, 3 pitidos, mensaje en el LCD; no se repite en 60 min) y que DHT11, LCD, buzzer, botón y LED
sigan como antes (pruebas H-01 a H-17 de `plan-integracion-esp32.md`).

## 9. Lista de verificación para cerrar el hardware

- [ ] Inscripción del relé, serigrafía y fotos del módulo MH-RD registradas.
- [ ] L-2: contactos del relé aislados de VCC y GND.
- [ ] L-4: tabla seco/mojado completada y contacto para D27 elegido.
- [ ] L-7: tres ciclos mojado/seco correctos, sin inestabilidad.
- [ ] D-1/D-2: tipo de LDR, resistencia y cableado a 3V3 confirmados.
- [ ] D-4/D-5: la luz responde, sin ruido, igual con WiFi; valores de calibración anotados.
- [ ] Voltaje de SDA/SCL del LCD medido (A-9).
- [ ] Cambios del firmware (si hacen falta) autorizados, aplicados y compilados.
- [ ] D-6 y alerta LLUVIA verificadas de extremo a extremo (ESP32 → API → PostgreSQL → React).
- [ ] H-01 a H-17 aprobadas con evidencia (fotos o capturas).
- [ ] 10 minutos de funcionamiento continuo sin lecturas rechazadas.
