// =====================================================================
//  ClimaPlus – Diagnóstico de la LDR en D34 (Fase 7)
//  Programa independiente: sin LCD ni API. No modifica el firmware principal ni prueba_ldr.
//  Amplía prueba_ldr con: detección de entrada flotante o saturada, prueba con el WiFi encendido,
//  la fórmula de luz ACTUAL del firmware y una sugerencia de calibración.
//
//  Circuito esperado (LDR suelta de 2 patas, divisor a 3.3 V; NUNCA a 5 V/VIN):
//
//      3V3 ── LDR ──┬── D34
//                   └── R_FIJA ── GND
//
//  Si tu LDR es un MÓDULO (pines VCC, GND, DO, AO): VCC a 3V3 (no 5 V), GND a GND, AO a D34 y DO sin conectar;
//  en ese caso pon LDR_ARRIBA = false (en esos módulos la LDR suele ir hacia GND: más luz = menos voltaje).
//
//  Monitor serie a 115200. Comandos (letra + Intro):
//    r = reiniciar mínimo y máximo    w = encender/apagar el WiFi (sin conectarse a ninguna red)
//    c = sugerir LDR_OSCURO / LDR_LUZ con el mínimo y el máximo observados
// =====================================================================
#include <Arduino.h>
#include <WiFi.h>

const uint8_t PIN_LDR    = 34;        // ADC1_CH6, solo entrada, sin pull-up/pull-down internos
const float   VREF       = 3.3f;
const float   R_FIJA     = 10000.0f;  // 10 kΩ = café-negro-NARANJA. 1 kΩ (café-negro-ROJO) → 1000.0f
const bool    LDR_ARRIBA = true;      // true: 3V3-LDR-D34-R-GND (más luz = más voltaje)
const int     MUESTRAS   = 32;

// Calibración que usa HOY climaplus_esp32_lcd.ino (para ver qué porcentaje enviaría).
const int FIRMWARE_LDR_OSCURO = 50;
const int FIRMWARE_LDR_LUZ    = 800;

int  minimo = 4095;
int  maximo = 0;
bool wifiEncendido = false;

void setup() {
  Serial.begin(115200);
  analogReadResolution(12);
  analogSetPinAttenuation(PIN_LDR, ADC_11db);   // rango útil aproximado 0,15–3,1 V
  WiFi.mode(WIFI_OFF);
  delay(500);
  Serial.println();
  Serial.println("=== Diagnostico de LDR en D34 (ADC1) ===");
  Serial.println("Tapa la LDR, luego iluminala con una lampara o la linterna del telefono.");
  Serial.println("Comandos: r = reiniciar min/max, w = WiFi on/off, c = sugerir calibracion");
}

void loop() {
  if (Serial.available()) {
    char c = Serial.read();
    if (c == 'r' || c == 'R') { minimo = 4095; maximo = 0; Serial.println("-- min/max reiniciados --"); }
    if (c == 'w' || c == 'W') {
      wifiEncendido = !wifiEncendido;
      WiFi.mode(wifiEncendido ? WIFI_STA : WIFI_OFF);
      Serial.printf("-- WiFi %s: en ADC1 (D34) las lecturas deben seguir iguales --\n", wifiEncendido ? "ENCENDIDO" : "APAGADO");
    }
    if (c == 'c' || c == 'C') {
      if (maximo - minimo < 200) {
        Serial.println("-- Rango muy pequeno: tapa y luego ilumina la LDR antes de pedir la sugerencia --");
      } else {
        int margen = (maximo - minimo) / 20;   // 5 % para que los extremos lleguen a 0 % y 100 %
        Serial.printf("-- Sugerencia para el firmware: LDR_OSCURO = %d; LDR_LUZ = %d; --\n", minimo + margen, maximo - margen);
      }
    }
  }

  // Promedio y dispersión de MUESTRAS lecturas.
  long suma = 0, sumaCuadrados = 0;
  for (int i = 0; i < MUESTRAS; i++) {
    int v = analogRead(PIN_LDR);
    suma += v;
    sumaCuadrados += (long)v * v;
    delayMicroseconds(300);
  }
  int   crudo      = suma / MUESTRAS;
  float desviacion = sqrtf(max(0.0f, (float)sumaCuadrados / MUESTRAS - (float)crudo * crudo));
  int   mv         = analogReadMilliVolts(PIN_LDR);
  if (crudo < minimo) minimo = crudo;
  if (crudo > maximo) maximo = crudo;

  // Resistencia estimada de la LDR según la posición en el divisor.
  float v = mv / 1000.0f;
  String rLdr;
  if (mv < 40 || mv > 3200) {
    rLdr = "fuera de rango";
  } else {
    float r = LDR_ARRIBA ? R_FIJA * (VREF - v) / v : R_FIJA * v / (VREF - v);
    rLdr = String(r / 1000.0f, 1) + " kOhm";
  }

  // Diagnóstico de la señal.
  const char *estado = "OK";
  if (desviacion > 60)        estado = "RUIDO ALTO: entrada flotante o cable flojo";
  else if (mv < 40)           estado = "~0 V: LDR abierta, sin 3V3 o D34 a GND";
  else if (mv > 3150)         estado = "SATURADO: D34 pegado a 3V3 o alimentado con 5 V";

  int luzFirmware = constrain(map(crudo, FIRMWARE_LDR_OSCURO, FIRMWARE_LDR_LUZ, 0, 100), 0, 100);

  Serial.printf("ADC=%4d (+-%3.0f)  V=%4d mV  LDR=%-14s luz_firmware=%3d%%  min=%4d max=%4d  WiFi=%s  %s\n",
                crudo, desviacion, mv, rLdr.c_str(), luzFirmware, minimo, maximo,
                wifiEncendido ? "on " : "off", estado);
  delay(500);
}
