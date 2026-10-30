// =====================================================================
//  ClimaPlus – Firmware del ESP32 (Fase 7)
//
//  Lee temperatura y humedad (BME280), lluvia (módulo LM393, salida digital) y luz (LDR),
//  y envía una lectura por minuto a POST /api/lecturas con la cabecera X-Device-Key.
//  Contrato: docs/api/contrato-dispositivos-lecturas.md
//
//  Alertas físicas: si la API responde alertas generadas, el LED se pone rojo y suena el buzzer.
//  LED: azul = enviando · verde = lectura aceptada · rojo = alerta · amarillo = error o sin WiFi.
//  Botón: pulsación corta = silenciar el buzzer · mantener 2 s = enviar una lectura ya.
//
//  Librerías (Gestor de librerías del IDE de Arduino):
//    Adafruit BME280 Library, Adafruit Unified Sensor, Adafruit SSD1306, Adafruit GFX Library,
//    ArduinoJson (v7). Solo para MODO_SIMULACION: DHT sensor library.
//  Placa: "DOIT ESP32 DEVKIT V1" (paquete esp32 de Espressif).
// =====================================================================

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <Wire.h>
#include <ArduinoJson.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#include "secrets.h"   // Copia secrets.example.h como secrets.h y llena tus datos.

#ifdef MODO_SIMULACION
  #include <DHT.h>
#else
  #include <Adafruit_BME280.h>
#endif

// ---------------------------------------------------------------------- Pines (lógica de 3.3 V)
const uint8_t PIN_SDA    = 21;   // I2C compartido: BME280 + OLED
const uint8_t PIN_SCL    = 22;
const uint8_t PIN_LLUVIA = 27;   // DO de la placa LM393
const uint8_t PIN_LDR    = 34;   // ADC1 (los pines ADC2 no leen con el WiFi activo)
const uint8_t PIN_LED_R  = 25;   // LED RGB de cátodo común, cada color con 220 Ω
const uint8_t PIN_LED_G  = 26;
const uint8_t PIN_LED_B  = 33;
const uint8_t PIN_BUZZER = 32;   // buzzer activo a través de un transistor NPN
const uint8_t PIN_BOTON  = 4;    // pulsador a GND, con INPUT_PULLUP
#ifdef MODO_SIMULACION
const uint8_t PIN_DHT    = 13;   // solo simulación (GPIO15 es pin de arranque)
#endif

// ---------------------------------------------------------------------- Ajustes
const unsigned long INTERVALO_MS        = 60000UL;  // una lectura por minuto
const unsigned long REINTENTO_WIFI_MS   = 10000UL;
const unsigned long PULSACION_LARGA_MS  = 2000UL;
const unsigned long DURACION_PITIDO_MS  = 150UL;
const uint16_t      TIMEOUT_HTTP_MS     = 8000;

// La placa LM393 pone su salida en LOW cuando detecta agua. Cámbialo a false si tu módulo funciona al revés.
const bool LLUVIA_ACTIVA_EN_BAJO = true;

// Calibración del LDR (valores crudos del ADC, 0–4095). Mira el monitor serie con el sensor tapado
// (oscuro) y con luz directa, y ajusta. map() funciona aunque el divisor esté conectado al revés.
const int LDR_OSCURO = 200;
const int LDR_LUZ    = 3800;

// ---------------------------------------------------------------------- Objetos
Adafruit_SSD1306 pantalla(128, 64, &Wire, -1);
#ifdef MODO_SIMULACION
DHT dht(PIN_DHT, DHT22);
#else
Adafruit_BME280 bme;
#endif

bool pantallaOk = false;
bool sensorOk   = false;

struct Lectura {
  bool  tieneTemperatura = false;
  float temperatura      = 0;
  bool  tieneHumedad     = false;
  float humedad          = 0;
  bool  lluvia           = false;
  int   luz              = 0;
};

Lectura       ultimaLectura;
int           ultimoCodigoHttp = 0;
String        ultimoEstado     = "Iniciando";
String        ultimaAlerta     = "";
unsigned long proximoEnvio     = 0;
unsigned long ultimoIntentoWifi = 0;

// Buzzer sin bloquear: cada pitido son dos cambios (encendido y apagado).
int           cambiosBuzzer   = 0;
unsigned long ultimoCambioBuzzer = 0;

// Botón
bool          botonPresionado = false;
unsigned long inicioPulsacion = 0;
bool          envioForzado    = false;

// Prototipos explícitos: el sketch compila igual en el IDE de Arduino, arduino-cli o PlatformIO.
void led(bool r, bool g, bool b);
void ledAzul();
void ledVerde();
void ledRojo();
void ledAmarillo();
void pitar(int veces);
void silenciar();
void atenderBuzzer();
void mostrar();
Lectura leerSensores();
void enviarLectura();
void atenderWifi();
void atenderBoton();

// ---------------------------------------------------------------------- LED y buzzer
void led(bool r, bool g, bool b) {
  digitalWrite(PIN_LED_R, r ? HIGH : LOW);
  digitalWrite(PIN_LED_G, g ? HIGH : LOW);
  digitalWrite(PIN_LED_B, b ? HIGH : LOW);
}
void ledAzul()     { led(false, false, true); }
void ledVerde()    { led(false, true,  false); }
void ledRojo()     { led(true,  false, false); }
void ledAmarillo() { led(true,  true,  false); }

void pitar(int veces) {
  cambiosBuzzer = veces * 2;
  ultimoCambioBuzzer = millis();
  digitalWrite(PIN_BUZZER, HIGH);
  cambiosBuzzer--;
}

void silenciar() {
  cambiosBuzzer = 0;
  digitalWrite(PIN_BUZZER, LOW);
}

void atenderBuzzer() {
  if (cambiosBuzzer > 0 && millis() - ultimoCambioBuzzer >= DURACION_PITIDO_MS) {
    digitalWrite(PIN_BUZZER, !digitalRead(PIN_BUZZER));
    ultimoCambioBuzzer = millis();
    cambiosBuzzer--;
  }
  if (cambiosBuzzer == 0) {
    digitalWrite(PIN_BUZZER, LOW);
  }
}

// ---------------------------------------------------------------------- Pantalla OLED
void mostrar() {
  if (!pantallaOk) return;
  pantalla.clearDisplay();
  pantalla.setTextSize(1);
  pantalla.setTextColor(SSD1306_WHITE);

  pantalla.setCursor(0, 0);
  pantalla.print("ClimaPlus ");
  pantalla.print(WiFi.status() == WL_CONNECTED ? "WiFi OK" : "Sin WiFi");

  pantalla.setCursor(0, 12);
  pantalla.print("T: ");
  if (ultimaLectura.tieneTemperatura) { pantalla.print(ultimaLectura.temperatura, 1); pantalla.print("C"); }
  else pantalla.print("--");
  pantalla.print("  H: ");
  if (ultimaLectura.tieneHumedad) { pantalla.print(ultimaLectura.humedad, 0); pantalla.print("%"); }
  else pantalla.print("--");

  pantalla.setCursor(0, 22);
  pantalla.print("Lluvia: ");
  pantalla.print(ultimaLectura.lluvia ? "Si" : "No");
  pantalla.print("  Luz: ");
  pantalla.print(ultimaLectura.luz);
  pantalla.print("%");

  pantalla.setCursor(0, 34);
  if (ultimoCodigoHttp > 0) { pantalla.print("HTTP "); pantalla.print(ultimoCodigoHttp); pantalla.print(" "); }
  pantalla.print(ultimoEstado.substring(0, 12));

  // Mensaje de la última alerta, en dos líneas de 21 caracteres.
  if (ultimaAlerta.length() > 0) {
    pantalla.setCursor(0, 46);
    pantalla.print(ultimaAlerta.substring(0, 21));
    pantalla.setCursor(0, 56);
    pantalla.print(ultimaAlerta.substring(21, 42));
  }
  pantalla.display();
}

// ---------------------------------------------------------------------- Sensores
Lectura leerSensores() {
  Lectura l;

#ifdef MODO_SIMULACION
  float t = dht.readTemperature();
  float h = dht.readHumidity();
#else
  float t = sensorOk ? bme.readTemperature() : NAN;
  float h = sensorOk ? bme.readHumidity()    : NAN;
#endif

  // Rangos de la API (RN-BD-31): un valor fuera de rango se omite en lugar de enviarse.
  if (!isnan(t) && t >= -40.0 && t <= 85.0) { l.tieneTemperatura = true; l.temperatura = t; }
  if (!isnan(h)) { l.tieneHumedad = true; l.humedad = constrain(h, 0.0f, 100.0f); }

  int estadoLluvia = digitalRead(PIN_LLUVIA);
  l.lluvia = LLUVIA_ACTIVA_EN_BAJO ? (estadoLluvia == LOW) : (estadoLluvia == HIGH);

  int crudo = analogRead(PIN_LDR);
  l.luz = constrain(map(crudo, LDR_OSCURO, LDR_LUZ, 0, 100), 0, 100);   // entero 0–100 (D-08)

  Serial.printf("Sensores: T=%s H=%s lluvia=%s luz=%d%% (ADC %d)\n",
                l.tieneTemperatura ? String(l.temperatura, 2).c_str() : "--",
                l.tieneHumedad ? String(l.humedad, 2).c_str() : "--",
                l.lluvia ? "si" : "no", l.luz, crudo);
  return l;
}

// ---------------------------------------------------------------------- Envío a la API
void enviarLectura() {
  ultimaLectura = leerSensores();

  if (WiFi.status() != WL_CONNECTED) {
    ultimoCodigoHttp = 0;
    ultimoEstado = "Sin WiFi";
    ledAmarillo();
    proximoEnvio = millis() + INTERVALO_MS;
    mostrar();
    return;
  }

  // Cuerpo JSON. Temperatura y humedad con 2 decimales exactos; la luz es entera.
  JsonDocument cuerpo;
  if (ultimaLectura.tieneTemperatura) cuerpo["temperatura"] = serialized(String(ultimaLectura.temperatura, 2));
  if (ultimaLectura.tieneHumedad)     cuerpo["humedad"]     = serialized(String(ultimaLectura.humedad, 2));
  cuerpo["lluvia"] = ultimaLectura.lluvia;
  cuerpo["luz"]    = ultimaLectura.luz;
  String json;
  serializeJson(cuerpo, json);

  ledAzul();
  ultimoEstado = "Enviando";
  mostrar();

  HTTPClient http;
  http.setTimeout(TIMEOUT_HTTP_MS);
  http.begin(API_URL);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Key", DEVICE_KEY);   // la clave nunca se imprime por el monitor serie
  const char* cabeceras[] = { "Retry-After" };
  http.collectHeaders(cabeceras, 1);

  int codigo = http.POST(json);
  String respuesta = codigo > 0 ? http.getString() : "";
  ultimoCodigoHttp = codigo;
  unsigned long espera = INTERVALO_MS;

  if (codigo == 201) {
    JsonDocument datos;
    JsonArray alertas;
    if (deserializeJson(datos, respuesta) == DeserializationError::Ok) {
      alertas = datos["alertasGeneradas"].as<JsonArray>();
    }
    if (!alertas.isNull() && alertas.size() > 0) {
      ultimaAlerta = alertas[0]["mensaje"].as<String>();
      ultimoEstado = "ALERTA";
      ledRojo();
      pitar(3);
      Serial.printf("Alerta: %s\n", ultimaAlerta.c_str());
    } else {
      ultimoEstado = "Enviada";
      ledVerde();
    }
  } else if (codigo == 401) {
    ultimoEstado = "Key invalida";
    ledAmarillo();
  } else if (codigo == 403) {
    ultimoEstado = "Desactivado";
    ledAmarillo();
  } else if (codigo == 429) {
    ultimoEstado = "Espera";
    ledAmarillo();
    long segundos = http.header("Retry-After").toInt();
    if (segundos > 0) espera = (unsigned long)segundos * 1000UL;
  } else if (codigo == 400) {
    ultimoEstado = "Datos inval.";
    ledAmarillo();
    Serial.println(respuesta);   // el detalle de validación ayuda a depurar
  } else {
    ultimoEstado = codigo > 0 ? "Error API" : "Sin respuesta";
    ledAmarillo();
  }

  Serial.printf("POST /api/lecturas -> %d (%s)\n", codigo, ultimoEstado.c_str());
  http.end();
  proximoEnvio = millis() + espera;
  mostrar();
}

// ---------------------------------------------------------------------- WiFi y botón
void atenderWifi() {
  if (WiFi.status() == WL_CONNECTED) return;
  if (millis() - ultimoIntentoWifi < REINTENTO_WIFI_MS) return;
  ultimoIntentoWifi = millis();
  Serial.println("WiFi: reconectando...");
  WiFi.disconnect();
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  ledAmarillo();
}

void atenderBoton() {
  bool presionado = digitalRead(PIN_BOTON) == LOW;
  if (presionado && !botonPresionado) {
    botonPresionado = true;
    inicioPulsacion = millis();
    envioForzado = false;
  } else if (presionado && botonPresionado && !envioForzado && millis() - inicioPulsacion >= PULSACION_LARGA_MS) {
    envioForzado = true;              // mantener 2 s: enviar ya
    Serial.println("Boton: envio inmediato");
    enviarLectura();
  } else if (!presionado && botonPresionado) {
    botonPresionado = false;
    if (!envioForzado && millis() - inicioPulsacion > 30) {   // pulsación corta (con antirrebote)
      silenciar();
      Serial.println("Boton: buzzer silenciado");
    }
  }
}

// ---------------------------------------------------------------------- setup / loop
void setup() {
  Serial.begin(115200);
  pinMode(PIN_LED_R, OUTPUT);
  pinMode(PIN_LED_G, OUTPUT);
  pinMode(PIN_LED_B, OUTPUT);
  pinMode(PIN_BUZZER, OUTPUT);
  pinMode(PIN_LLUVIA, INPUT_PULLUP);
  pinMode(PIN_BOTON, INPUT_PULLUP);
  analogReadResolution(12);
  ledAmarillo();

  Wire.begin(PIN_SDA, PIN_SCL);
  pantallaOk = pantalla.begin(SSD1306_SWITCHCAPVCC, 0x3C);
  if (!pantallaOk) Serial.println("OLED no encontrada (0x3C)");

#ifdef MODO_SIMULACION
  dht.begin();
  sensorOk = true;
#else
  sensorOk = bme.begin(0x76) || bme.begin(0x77);
  if (!sensorOk) Serial.println("BME280 no encontrado (0x76/0x77): se enviaran solo lluvia y luz");
#endif

  ultimoEstado = "Conectando";
  mostrar();
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  ultimoIntentoWifi = millis();

  // Espera hasta 15 s la primera conexión; si no, el loop sigue reintentando.
  while (WiFi.status() != WL_CONNECTED && millis() - ultimoIntentoWifi < 15000UL) {
    delay(250);
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("WiFi conectado. IP del ESP32: %s\n", WiFi.localIP().toString().c_str());
    ledVerde();
  }
  proximoEnvio = millis() + 2000;   // primera lectura a los 2 s
}

void loop() {
  atenderWifi();
  atenderBoton();
  atenderBuzzer();

  if ((long)(millis() - proximoEnvio) >= 0) {
    enviarLectura();
  }
}
