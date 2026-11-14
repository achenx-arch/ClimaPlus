# ClimaPlus – Hardware oficial: ESP32 DevKit con LCD

El ESP32 mide temperatura, humedad, lluvia y luz y envía una lectura cada **10 segundos** a la API de ClimaPlus
(`POST /api/lecturas`, cabecera `X-Device-Key`). Si la API responde que se generó una alerta, el ESP32 muestra el
mensaje en la pantalla LCD, enciende el LED amarillo y hace sonar el buzzer.

- Firmware oficial: [`climaplus_esp32_lcd/climaplus_esp32_lcd.ino`](climaplus_esp32_lcd/climaplus_esp32_lcd.ino).
- Contrato de la API: [`docs/api/contrato-dispositivos-lecturas.md`](../../docs/api/contrato-dispositivos-lecturas.md).
- Plan de integración y pruebas finales: [`docs/hardware/plan-integracion-esp32.md`](../../docs/hardware/plan-integracion-esp32.md).

```
hardware/esp32/
├── climaplus_esp32_lcd/          ← MONTAJE OFICIAL (entrega final)
│   ├── climaplus_esp32_lcd.ino   # firmware
│   ├── secrets.example.h         # plantilla de configuración (sí se sube)
│   └── secrets.h                 # tu WiFi y API Key reales (NO se sube: está en .gitignore)
├── prueba_ldr/prueba_ldr.ino     # prueba aislada de la LDR (calibración)
├── caja/                         # diseño de la caja (OpenSCAD, medidas y maqueta)
├── climaplus_esp32/              # versión anterior (BME280 + OLED): NO forma parte del montaje oficial
└── wokwi/                        # simulación de la versión anterior (BME280/DHT22 + OLED): no representa el montaje oficial
```

## 1. Componentes del montaje oficial

| # | Componente | Uso en el firmware |
|---|---|---|
| 1 | ESP32 DevKit (placa "DOIT ESP32 DEVKIT V1") | Controlador y WiFi |
| 2 | Sensor DHT11 | Temperatura y humedad (`TIPO_DHT DHT11`) |
| 3 | Sensor LDR (fotorresistencia) + resistencia fija del divisor | Luz, convertida a 0–100 % |
| 4 | Sensor de lluvia (placa comparadora LM393, salida digital DO) | Lluvia sí/no |
| 5 | Pantalla LCD 16x2 con módulo I2C (PCF8574) | Mediciones, estado y alertas |
| 6 | Buzzer | Aviso sonoro de alerta (3 pitidos) |
| 7 | Botón (pulsador) | Silenciar el buzzer o enviar una lectura ya |
| 8 | Tres LED: rojo, verde y amarillo, cada uno con su resistencia | Estado del envío |

No se usan BME280, OLED ni LED RGB: esos componentes eran de la versión anterior.

## 2. Conexiones (pines definidos en el firmware)

| Componente | Pin del componente | Pin del ESP32 | Nota |
|---|---|---|---|
| LCD (módulo I2C) | SDA / SCL | GPIO21 / GPIO22 | Dirección 0x27 o 0x3F, se detecta sola al arrancar |
| DHT11 | DATA | GPIO23 | VCC a 3V3. Si es el sensor suelto de 4 patas, resistencia de 10 kΩ entre DATA y VCC (el módulo de 3 pines ya la trae) |
| Sensor de lluvia (LM393) | DO | GPIO27 | Se recomienda alimentar la placa con 3V3 |
| LDR | divisor: 3V3 → LDR → GPIO34 → resistencia → GND | GPIO34 (ADC1) | Ver `prueba_ldr` para calibrar |
| LED rojo / verde / amarillo | ánodo (con su resistencia de 220 Ω) | GPIO25 / GPIO26 / GPIO33 | Cátodo a GND |
| Buzzer | a través de un transistor NPN | GPIO32 | El firmware lo trata como buzzer **activo** (HIGH = suena) |
| Botón | un lado al pin, el otro a GND | GPIO4 | `INPUT_PULLUP`: no necesita resistencia externa |

Reglas que respeta el firmware:
- La LDR va en un pin **ADC1** (GPIO32–39): los pines ADC2 no leen mientras el WiFi está activo.
- No se usan GPIO0, 2, 12 ni 15 (intervienen en el arranque).
- La placa de lluvia pone `DO` en `LOW` cuando detecta agua (`LLUVIA_ACTIVA_EN_BAJO = true`). Si tu módulo funciona al
  revés, cambia esa constante a `false`.

**Voltaje del LCD.** Muchos módulos I2C del LCD se alimentan con 5 V (VIN) y traen resistencias pull-up hacia 5 V en
SDA y SCL, mientras que los pines del ESP32 trabajan a 3.3 V. Comprueba con un multímetro el voltaje de SDA y SCL
respecto a GND con el LCD conectado: si marca unos 5 V, usa un convertidor de nivel I2C (o retira las pull-up del
módulo) para no forzar los pines del ESP32.

## 3. Comportamiento

| Señal | Significado |
|---|---|
| Al encender: rojo, verde y amarillo uno por uno | Prueba de los LED |
| **Verde (OK)** | Lectura aceptada (201), sin alertas (también mientras se envía) |
| **Amarillo (ALERTA) + 3 pitidos** | La API generó una alerta: un valor salió del rango configurado; el mensaje aparece en el LCD |
| **Rojo (ERROR)** | Sin WiFi, DHT sin lectura, sin respuesta de la API o certificado HTTPS no válido, API Key inválida (401), dispositivo desactivado (403), demasiadas peticiones (429) o datos inválidos (400) |
| Prioridad | ERROR > ALERTA > OK (un fallo del DHT deja el rojo aunque la API haya aceptado la lectura) |
| Botón, pulsación corta | Silencia el buzzer |
| Botón, mantener 2 s | Envía una lectura de inmediato |

El LCD cambia de página cada 3 s:

| Página | Línea 1 | Línea 2 |
|---|---|---|
| Mediciones | `T:24.5C H:68%` | `Lluvia:No L:72%` |
| Estado | `WiFi OK HTTP201` | `Lectura enviada`, `API Key invalida`, `Espera (429)`… |
| Alerta (solo si hubo una) | Primeros 16 caracteres del mensaje | Siguientes 16 caracteres (sin tildes) |

- Una lectura cada 10 s (`INTERVALO_MS`); la API admite hasta 30 por minuto por dispositivo. Ante un 429 espera lo
  que indique la cabecera `Retry-After`.
- Temperatura y humedad se envían con 2 decimales; la luz es un entero de 0 a 100 (D-08).
- Si el DHT11 no responde, temperatura y humedad se omiten: lluvia y luz siempre van, así que la API acepta la lectura.
- La fecha la pone el servidor: el ESP32 no necesita reloj.
- La API Key **nunca** se imprime por el monitor serie.

## 4. Puesta en marcha

### 4.1 Librerías y placa (IDE de Arduino)
Gestor de librerías: **LiquidCrystal I2C** (Frank de Brabander), **DHT sensor library** (Adafruit),
**Adafruit Unified Sensor** y **ArduinoJson** (versión 7).
Placa: *Herramientas → Placa → esp32 → DOIT ESP32 DEVKIT V1* (paquete "esp32" de Espressif).

### 4.2 Obtener la API Key
1. Arranca la API y el frontend, inicia sesión (correo, contraseña y código) y entra a **Mis dispositivos**.
2. Pulsa **Registrar dispositivo**, escribe el nombre y elige la ubicación.
3. Copia la API Key (`cpk_…`) del modal. Solo se muestra esa vez; si la pierdes, desactiva el dispositivo y
   registra otro.

### 4.3 Configurar `secrets.h`
Copia `climaplus_esp32_lcd/secrets.example.h` como `climaplus_esp32_lcd/secrets.h` y llena:
- `WIFI_SSID` y `WIFI_PASSWORD`: red de **2.4 GHz**.
- `API_URL`: `http://<IP de tu PC>:8080/api/lecturas` con Docker, o `http://<IP de tu PC>:5208/api/lecturas` con
  `dotnet run`. La IP se ve con `ipconfig` (Dirección IPv4). No uses `localhost`: para el ESP32, localhost es el
  propio ESP32.
- `DEVICE_KEY`: la API Key del paso anterior.
- `TIPO_DHT`: `DHT11`.

### 4.4 Que el ESP32 alcance la API
- **Docker:** el puerto 8080 ya escucha en toda la red local.
- **`dotnet run`:** por defecto solo escucha en `localhost`. Para la red local:

  ```powershell
  dotnet run --project backend/ClimaPlus.Api --launch-profile http --urls http://0.0.0.0:5208
  ```

En ambos casos, permite el puerto en el Firewall de Windows (PowerShell como administrador, una sola vez; usa 8080
o 5208 según lo que uses):

```powershell
New-NetFirewallRule -DisplayName "ClimaPlus API" -Direction Inbound -Protocol TCP -LocalPort 8080,5208 -Action Allow -Profile Private
```

La PC y el ESP32 deben estar en la misma red WiFi (perfil de red "Privada" en Windows).

### 4.5 Subir y probar
1. Sube el sketch y abre el monitor serie a **115200** baudios.
2. Verás la IP del ESP32 y, cada 10 s, `POST /api/lecturas -> 201 (Lectura enviada)`.
3. En **Mis dispositivos** el dispositivo aparece **En línea** y en su detalle (M12) se ven las lecturas.
4. Para probar la alerta física: crea una regla de temperatura alta con un umbral menor que la temperatura
   ambiente. En la siguiente lectura el LED se pone amarillo, suena el buzzer y la alerta aparece en M09 y M12.
   La API no repite una alerta del mismo tipo para esa ubicación durante 60 minutos.

### 4.6 Calibrar la LDR
Sube `prueba_ldr/prueba_ldr.ino`, abre el monitor serie y anota el valor crudo con el sensor tapado y con luz
directa. Pon esos valores en `LDR_OSCURO` y `LDR_LUZ` del firmware y vuelve a subir `climaplus_esp32_lcd.ino`.

## 5. Seguridad

- `secrets.h` (WiFi y API Key) nunca se sube a GitHub.
- En la red local el ESP32 puede usar HTTP (la API Key viaja sin cifrar dentro de tu WiFi). Con `API_URL` en
  `https://…` el firmware valida el certificado del servidor contra las raíces de Let's Encrypt de `ca_raiz.h`
  (nunca `setInsecure`): si no coincide, no envía nada y enciende el rojo. Detalles: `docs/hardware/esp32-https.md`.
- Si una API Key se filtra: desactiva el dispositivo en **Mis dispositivos** (el ESP32 recibirá 403) y registra otro.

## 6. Simulación

La carpeta `wokwi/` simula la **versión anterior** (OLED, LED RGB, DHT22): sirve como referencia histórica, pero no
representa el montaje oficial. Las pruebas de la entrega final se hacen con la placa real, o simulando el ESP32
desde PowerShell (sección "Prueba manual sin hardware" del contrato de la API).
