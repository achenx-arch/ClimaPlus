# ClimaPlus – Firmware del ESP32 (Fase 7)

El ESP32 mide temperatura, humedad, lluvia y luz, y cada minuto envía una lectura a la API de ClimaPlus
(`POST /api/lecturas`, cabecera `X-Device-Key`). Si la API responde que se generó una alerta, el ESP32 la
muestra en la pantalla, enciende el LED rojo y hace sonar el buzzer.
Contrato de la API: [`docs/api/contrato-dispositivos-lecturas.md`](../../docs/api/contrato-dispositivos-lecturas.md).

```
hardware/esp32/
├── climaplus_esp32/
│   ├── climaplus_esp32.ino     # firmware
│   ├── secrets.example.h       # plantilla de configuración (sí se sube)
│   └── secrets.h               # tu WiFi y API Key reales (NO se sube: está en .gitignore)
└── wokwi/                      # simulación (diagram.json, wokwi.toml, libraries.txt)
```

**Compilación verificada:** `arduino-cli compile --fqbn esp32:esp32:esp32doit-devkit-v1` con el paquete esp32 3.0.7
de Espressif, sin errores ni advertencias (83 % del flash y 14 % de la RAM), en modo real y en modo simulación.

## 1. Materiales

| # | Material | Cant. |
|---|---|---|
| 1 | ESP32 DevKit V1 (ESP32-WROOM-32) y cable USB de datos | 1 |
| 2 | Módulo BME280 I2C 3.3 V (no BMP280: ese no mide humedad) | 1 |
| 3 | Sensor de lluvia YL-83 / FC-37 con placa LM393 | 1 |
| 4 | Fotorresistencia LDR 5 mm (GL5528) y resistencia de 10 kΩ | 1 + 1 |
| 5 | Pantalla OLED 0.96" SSD1306 I2C (4 pines) | 1 |
| 6 | LED RGB 5 mm cátodo común y 3 resistencias de 220 Ω | 1 + 3 |
| 7 | Buzzer activo 3.3–5 V, transistor NPN 2N2222/S8050 y resistencia de 1 kΩ | 1 + 1 + 1 |
| 8 | Pulsador 6 × 6 mm | 1 |
| 9 | Protoboard de 830 puntos y cables Dupont | 1 |
| 10 | Cargador USB 5 V / 2 A (para usarlo sin la computadora) | 1 |

## 2. Conexiones (todo a 3.3 V)

| Componente | Pin del módulo | Pin del ESP32 |
|---|---|---|
| BME280 y OLED (comparten el bus I2C) | SDA / SCL / VCC / GND | GPIO21 / GPIO22 / 3V3 / GND |
| Sensor de lluvia (placa LM393) | DO / VCC / GND | GPIO27 / 3V3 / GND |
| LDR | un extremo a 3V3, el otro al GPIO34; resistencia de 10 kΩ del GPIO34 a GND | GPIO34 (ADC1) |
| LED RGB | R / G / B, cada uno con 220 Ω; pata común a GND | GPIO25 / GPIO26 / GPIO33 |
| Buzzer | base del transistor con 1 kΩ; buzzer entre 3V3 y el colector; emisor a GND | GPIO32 |
| Pulsador | un lado al pin, el otro a GND | GPIO4 (`INPUT_PULLUP`) |

Notas:
- El LDR va en un pin **ADC1** (32–39): los pines ADC2 no leen mientras el WiFi está activo.
- No uses GPIO0, 2, 12 ni 15: intervienen en el arranque del ESP32.
- Alimenta la placa LM393 con **3.3 V** para que su salida no supere el voltaje que tolera el ESP32.
- La placa LM393 pone su salida en `LOW` cuando detecta agua (`LLUVIA_ACTIVA_EN_BAJO = true`). Si tu módulo
  funciona al revés, cambia esa constante a `false`.

## 3. Comportamiento

| Señal | Significado |
|---|---|
| LED azul | Enviando una lectura |
| LED verde | Lectura aceptada (201), sin alertas |
| LED rojo + 3 pitidos | La API generó una alerta; el mensaje aparece en la OLED |
| LED amarillo | Sin WiFi, API Key inválida (401), dispositivo desactivado (403), demasiadas peticiones (429) o datos inválidos (400) |
| Botón, pulsación corta | Silencia el buzzer |
| Botón, mantener 2 s | Envía una lectura de inmediato |

- Una lectura por minuto (`INTERVALO_MS`). Ante un 429 espera lo que indique la cabecera `Retry-After`.
- Temperatura y humedad se envían con 2 decimales; la luz es un entero de 0 a 100 (D-08).
- Si un sensor falla, ese campo se omite: la API acepta lecturas con al menos una medición.
- La fecha la pone el servidor: el ESP32 no necesita reloj.
- La API Key **nunca** se imprime por el monitor serie.

## 4. Puesta en marcha

### 4.1 Librerías (IDE de Arduino → Gestor de librerías)
Adafruit BME280 Library, Adafruit Unified Sensor, Adafruit SSD1306, Adafruit GFX Library, ArduinoJson (versión 7)
y, solo para la simulación, DHT sensor library.
Placa: *Herramientas → Placa → esp32 → DOIT ESP32 DEVKIT V1* (paquete "esp32" de Espressif).

### 4.2 Obtener la API Key
1. Arranca la API y el frontend, inicia sesión y entra a **Mis dispositivos**.
2. Pulsa **Registrar dispositivo**, escribe el nombre y elige la ubicación.
3. Copia la API Key (`cpk_…`) del modal. Solo se muestra esa vez; si la pierdes, desactiva el dispositivo y
   registra otro.

### 4.3 Configurar `secrets.h`
Copia `climaplus_esp32/secrets.example.h` como `climaplus_esp32/secrets.h` y llena:
- `WIFI_SSID` y `WIFI_PASSWORD`: red de **2.4 GHz**.
- `API_URL`: `http://<IP de tu PC>:5208/api/lecturas`. La IP se ve con `ipconfig` (Dirección IPv4). No uses
  `localhost`: para el ESP32, localhost es el propio ESP32.
- `DEVICE_KEY`: la API Key del paso anterior.

### 4.4 Arrancar la API para que el ESP32 la alcance
Por defecto la API solo escucha en `localhost`. Para la red local:

```bash
dotnet run --project backend/ClimaPlus.Api --launch-profile http --urls http://0.0.0.0:5208
```

Y permite el puerto en el Firewall de Windows (PowerShell como administrador, una sola vez):

```powershell
New-NetFirewallRule -DisplayName "ClimaPlus API 5208" -Direction Inbound -Protocol TCP -LocalPort 5208 -Action Allow
```

La PC y el ESP32 deben estar en la misma red WiFi.

### 4.5 Subir y probar
1. Sube el sketch y abre el monitor serie a **115200** baudios.
2. Verás la IP del ESP32 y, cada minuto, `POST /api/lecturas -> 201 (Enviada)`.
3. En **Mis dispositivos** el dispositivo aparece **En línea** y en su detalle (M12) se ven las lecturas.
4. Para probar la alerta física: crea una regla de temperatura alta con un umbral menor que la temperatura
   ambiente. En la siguiente lectura el LED se pone rojo, suena el buzzer y la alerta aparece en M09 y M12.

### 4.6 Calibrar el LDR
El monitor serie muestra el valor crudo del ADC (`ADC 1234`). Anota el valor con el sensor tapado y con luz
directa, y ajústalos en `LDR_OSCURO` y `LDR_LUZ` dentro del sketch.

## 5. Simulación en Wokwi (sin hardware)

Wokwi no tiene BME280: la simulación usa un **DHT22** (GPIO13) activando `MODO_SIMULACION` en `secrets.h`.
El interruptor deslizante simula la lluvia y el sensor de luz de Wokwi reemplaza al LDR.

- **wokwi.com:** crea un proyecto ESP32, pega `climaplus_esp32.ino`, agrega una pestaña `secrets.h` (con
  `#define MODO_SIMULACION`, `WIFI_SSID "Wokwi-GUEST"` y `WIFI_PASSWORD ""`), reemplaza `diagram.json` y agrega
  las librerías de `wokwi/libraries.txt`.
- **VS Code:** con la extensión Wokwi, compila con
  `arduino-cli compile --fqbn esp32:esp32:esp32doit-devkit-v1 --output-dir climaplus_esp32/build climaplus_esp32`
  y abre `wokwi/diagram.json`.

Limitación: el simulador no puede llegar a la API que corre en tu PC (`localhost` o tu IP local). En Wokwi se
comprueban los sensores, la OLED, el LED, el buzzer y el botón; el envío a la API se prueba con la placa real o
simulando el ESP32 desde PowerShell (ver el contrato de la API).
