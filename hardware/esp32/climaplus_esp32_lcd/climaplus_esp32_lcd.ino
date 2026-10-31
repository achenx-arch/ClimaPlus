// =====================================================================
//  ClimaPlus – Firmware del ESP32 · versión LCD 16x2 I2C + DHT + 3 LED (Fase 7)
//
//  Lee temperatura y humedad (DHT11 o DHT22), lluvia (módulo LM393, salida digital) y luz (LDR),
//  y envía una lectura por minuto a POST /api/lecturas con la cabecera X-Device-Key.
//  Contrato: docs/api/contrato-dispositivos-lecturas.md
//
//  LED:  verde = lectura aceptada · rojo = alerta (suena el buzzer) · amarillo = error o sin WiFi
//        verde + amarillo encendidos = enviando
//  LCD:  alterna cada 3 s entre las mediciones y el estado (WiFi, HTTP y mensaje de la última alerta).
//  Botón: pulsación corta = silenciar el buzzer · mantener 2 s = enviar una lectura ya.
//
//  Librerías (Herramientas → Gestionar bibliotecas):
//    "LiquidCrystal I2C" (Frank de Brabander), "DHT sensor library" (Adafruit),
//    "Adafruit Unified Sensor" y "ArduinoJson" (versión 7).
//  Placa: "DOIT ESP32 DEVKIT V1" (paquete esp32 de Espressif).
// =====================================================================

#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <Wire.h>
#include <ArduinoJson.h>
#include <LiquidCrystal_I2C.h>
#include <DHT.h>

#include "secrets.h"   // Copia secrets.example.h como secrets.h y llena tus datos.

#ifndef TIPO_DHT
#define TIPO_DHT DHT11
#endif

// ---------------------------------------------------------------------- Pines (lógica de 3.3 V)
const uint8_t PIN_SDA         = 21;   // LCD (módulo PCF8574)
const uint8_t PIN_SCL         = 22;
const uint8_t PIN_DHT         = 23;   // DATA del DHT
const uint8_t PIN_LLUVIA      = 27;   // DO de la placa LM393
const uint8_t PIN_LDR         = 34;   // ADC1 (los pines ADC2 no leen con el WiFi activo)
const uint8_t PIN_LED_ROJO    = 25;   // cada LED con su resistencia de 220 Ω
const uint8_t PIN_LED_VERDE   = 26;
const uint8_t PIN_LED_AMARILLO = 33;
const uint8_t PIN_BUZZER      = 32;   // buzzer activo a través de un transistor NPN
const uint8_t PIN_BOTON       = 4;    // pulsador a GND, con INPUT_PULLUP

// ---------------------------------------------------------------------- Ajustes
const unsigned long INTERVALO_MS        = 60000UL;  // una lectura por minuto
const unsigned long REINTENTO_WIFI_MS   = 10000UL;
const unsigned long PULSACION_LARGA_MS  = 2000UL;
const unsigned long DURACION_PITIDO_MS  = 150UL;
const unsigned long CAMBIO_PANTALLA_MS  = 3000UL;
const uint16_t      TIMEOUT_HTTP_MS     = 8000;

// La placa LM393 pone su salida en LOW cuando detecta agua. Cámbialo a false si tu módulo funciona al revés.
const bool LLUVIA_ACTIVA_EN_BAJO = true;

// Calibración del LDR (valores crudos del ADC, 0–4095): mira el monitor serie tapando y destapando el sensor.
const int LDR_OSCURO = 200;
const int LDR_LUZ    = 3800;

// ---------------------------------------------------------------------- Objetos
LiquidCrystal_I2C *lcd = nullptr;   // la dirección (0x27 o 0x3F) se detecta al arrancar
DHT dht(PIN_DHT, TIPO_DHT);

struct Lectura {
  bool  tieneTemperatura = false;
  float temperatura      = 0;
  bool  tieneHumedad     = false;
  float humedad          = 0;
  bool  lluvia           = false;
  int   luz              = 0;
};

Lectura       ultimaLectura;
int           ultimoCodigoHttp  = 0;
String        ultimoEstado      = "Iniciando";
String        ultimaAlerta      = "";
unsigned long proximoEnvio      = 0;
unsigned long ultimoIntentoWifi = 0;
unsigned long ultimoCambioPantalla = 0;
uint8_t       paginaPantalla    = 0;

int           cambiosBuzzer      = 0;
unsigned long ultimoCambioBuzzer = 0;

bool          botonPresionado = false;
unsigned long inicioPulsacion = 0;
bool          envioForzado    = false;

// Prototipos explícitos: el sketch compila igual en el IDE de Arduino, arduino-cli o PlatformIO.
void leds(bool rojo, bool verde, bool amarillo);
void pitar(int veces);
void silenciar();
void atenderBuzzer();
void mostrar();
Lectura leerSensores();
void enviarLectura();
void atenderWifi();
void atenderBoton();

// ---------------------------------------------------------------------- LED y buzzer
void leds(bool rojo, bool verde, bool amarillo) {
  digitalWrite(PIN_LED_ROJO, rojo ? HIGH : LOW);
  digitalWrite(PIN_LED_VERDE, verde ? HIGH : LOW);
  digitalWrite(PIN_LED_AMARILLO, amarillo ? HIGH : LOW);
}
void ledEnviando() { leds(false, true,  true);  }
void ledOk()       { leds(false, true,  false); }
void ledAlerta()   { leds(true,  false, false); }
void ledError()    { leds(false, false, true);  }

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

// ---------------------------------------------------------------------- Pantalla LCD 16x2
// Escribe una línea completa de 16 caracteres (rellena con espacios para borrar lo anterior).
void linea(uint8_t fila, String texto) {
  if (!lcd) return;
  if (texto.length() > 16) texto = texto.substring(0, 16);
  while (texto.length() < 16) texto += ' ';
  lcd->setCursor(0, fila);
  lcd->print(texto);
}

// El LCD no tiene tildes ni ñ: se reemplazan para que el mensaje se lea bien.
String sinTildes(String s) {
  const char *de[]   = { "á", "é", "í", "ó", "ú", "Á", "É", "Í", "Ó", "Ú", "ñ", "Ñ", "ü", "°" };
  const char *a_[]   = { "a", "e", "i", "o", "u", "A", "E", "I", "O", "U", "n", "N", "u", ""  };
  for (int i = 0; i < 14; i++) s.replace(de[i], a_[i]);
  return s;
}

void mostrar() {
  if (!lcd) return;
  if (paginaPantalla == 0) {
    // Página 1: mediciones.  "T:24.5C H:68%" / "Lluvia:No L:72%"
    String l1 = "T:" + (ultimaLectura.tieneTemperatura ? String(ultimaLectura.temperatura, 1) + "C" : String("--"));
    l1 += " H:" + (ultimaLectura.tieneHumedad ? String((int)round(ultimaLectura.humedad)) + "%" : String("--"));
    String l2 = String("Lluvia:") + (ultimaLectura.lluvia ? "Si" : "No") + " L:" + String(ultimaLectura.luz) + "%";
    linea(0, l1);
    linea(1, l2);
  } else if (ultimaAlerta.length() > 0 && paginaPantalla == 2) {
    // Página 3 (solo si hubo alerta): el mensaje en dos líneas de 16 caracteres.
    String m = sinTildes(ultimaAlerta);
    linea(0, m.substring(0, 16));
    linea(1, m.length() > 16 ? m.substring(16, 32) : "");
  } else {
    // Página 2: estado.  "WiFi OK  HTTP201" / "Enviada"
    String l1 = WiFi.status() == WL_CONNECTED ? "WiFi OK" : "Sin WiFi";
    if (ultimoCodigoHttp != 0) l1 += " HTTP" + String(ultimoCodigoHttp);
    linea(0, l1);
    linea(1, ultimoEstado);
  }
}

void atenderPantalla() {
  if (millis() - ultimoCambioPantalla < CAMBIO_PANTALLA_MS) return;
  ultimoCambioPantalla = millis();
  uint8_t paginas = ultimaAlerta.length() > 0 ? 3 : 2;
  paginaPantalla = (paginaPantalla + 1) % paginas;
  mostrar();
}

// Busca el LCD en las direcciones habituales del módulo PCF8574 (0x27 o 0x3F).
void iniciarPantalla() {
  const uint8_t direcciones[] = { 0x27, 0x3F };
  for (uint8_t dir : direcciones) {
    Wire.beginTransmission(dir);
    if (Wire.endTransmission() == 0) {
      lcd = new LiquidCrystal_I2C(dir, 16, 2);
      lcd->init();
      lcd->backlight();
      Serial.printf("LCD encontrado en 0x%02X\n", dir);
      return;
    }
  }
  Serial.println("LCD no encontrado (0x27 / 0x3F): revisa SDA=D21, SCL=D22 y la alimentacion");
}

// ---------------------------------------------------------------------- Sensores
Lectura leerSensores() {
  Lectura l;

  float t = dht.readTemperature();
  float h = dht.readHumidity();

  // Rangos de la API (RN-BD-31): un valor inválido se omite en lugar de enviarse.
  if (!isnan(t) && t >= -40.0 && t <= 85.0) { l.tieneTemperatura = true; l.temperatura = t; }
  if (!isnan(h)) { l.tieneHumedad = true; l.humedad = constrain(h, 0.0f, 100.0f); }

  int estadoLluvia = digitalRead(PIN_LLUVIA);
  l.lluvia = LLUVIA_ACTIVA_EN_BAJO ? (estadoLluvia == LOW) : (estadoLluvia == HIGH);

  int crudo = analogRead(PIN_LDR);
  l.luz = constrain(map(crudo, LDR_OSCURO, LDR_LUZ, 0, 100), 0, 100);   // entero 0–100 (D-08)

  if (isnan(t) || isnan(h)) Serial.println("DHT sin lectura: revisa DATA=D23 y TIPO_DHT (DHT11/DHT22)");
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
    ledError();
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

  ledEnviando();
  ultimoEstado = "Enviando...";
  paginaPantalla = 1;
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
      ultimoEstado = "ALERTA!";
      ledAlerta();
      pitar(3);
      paginaPantalla = 2;   // mostrar el mensaje de la alerta de inmediato
      Serial.printf("Alerta: %s\n", ultimaAlerta.c_str());
    } else {
      ultimoEstado = "Lectura enviada";
      ledOk();
    }
  } else if (codigo == 401) {
    ultimoEstado = "API Key invalida";
    ledError();
  } else if (codigo == 403) {
    ultimoEstado = "Desactivado";
    ledError();
  } else if (codigo == 429) {
    ultimoEstado = "Espera (429)";
    ledError();
    long segundos = http.header("Retry-After").toInt();
    if (segundos > 0) espera = (unsigned long)segundos * 1000UL;
  } else if (codigo == 400) {
    ultimoEstado = "Datos invalidos";
    ledError();
    Serial.println(respuesta);
  } else {
    ultimoEstado = codigo > 0 ? "Error de la API" : "Sin respuesta";
    ledError();
  }

  Serial.printf("POST /api/lecturas -> %d (%s)\n", codigo, ultimoEstado.c_str());
  http.end();
  proximoEnvio = millis() + espera;
  ultimoCambioPantalla = millis();
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
  ledError();
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
  pinMode(PIN_LED_ROJO, OUTPUT);
  pinMode(PIN_LED_VERDE, OUTPUT);
  pinMode(PIN_LED_AMARILLO, OUTPUT);
  pinMode(PIN_BUZZER, OUTPUT);
  pinMode(PIN_LLUVIA, INPUT_PULLUP);
  pinMode(PIN_BOTON, INPUT_PULLUP);
  analogReadResolution(12);

  // Prueba de LED al encender: rojo, verde y amarillo, uno por uno.
  leds(true, false, false); delay(250);
  leds(false, true, false); delay(250);
  leds(false, false, true); delay(250);
  ledError();

  Wire.begin(PIN_SDA, PIN_SCL);
  iniciarPantalla();
  linea(0, "ClimaPlus ESP32");
  linea(1, "Conectando WiFi");

  dht.begin();

  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  ultimoIntentoWifi = millis();

  // Espera hasta 15 s la primera conexión; si no, el loop sigue reintentando.
  while (WiFi.status() != WL_CONNECTED && millis() - ultimoIntentoWifi < 15000UL) {
    delay(250);
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("WiFi conectado. IP del ESP32: %s\n", WiFi.localIP().toString().c_str());
    linea(0, "WiFi conectado");
    linea(1, WiFi.localIP().toString());
    ledOk();
  } else {
    linea(0, "Sin WiFi");
    linea(1, "Revisa secrets.h");
  }
  proximoEnvio = millis() + 3000;   // primera lectura a los 3 s (el DHT necesita unos segundos)
}

void loop() {
  atenderWifi();
  atenderBoton();
  atenderBuzzer();
  atenderPantalla();

  if ((long)(millis() - proximoEnvio) >= 0) {
    enviarLectura();
  }
}
