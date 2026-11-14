// =====================================================================
//  ClimaPlus – Prueba aislada de la fotoresistencia (LDR) en D34
//  Solo lee la LDR: sin WiFi, sin LCD, sin API.
//
//  Circuito:   3V3 ── LDR ──┬── D34
//                           └── 10 kΩ ── GND
//
//  Abre el Monitor serie a 115200 y tapa / destapa la LDR.
//  Escribe "r" + Intro para reiniciar el mínimo y el máximo.
//  Cuando termines, vuelve a subir climaplus_esp32_lcd.ino.
// =====================================================================
#include <Arduino.h>

const uint8_t PIN_LDR      = 34;
const float   VREF         = 3.3f;     // voltaje de alimentación del divisor
const float   R_FIJA       = 10000.0f; // 10 kΩ = café-negro-NARANJA. Si usas café-negro-ROJO (1 kΩ), pon 1000.0f
const int     MUESTRAS     = 16;       // promedio para quitar ruido

int leerPromedio();   // prototipo explícito

int minimo = 4095;
int maximo = 0;

int leerPromedio() {
  long suma = 0;
  for (int i = 0; i < MUESTRAS; i++) {
    suma += analogRead(PIN_LDR);
    delayMicroseconds(200);
  }
  return suma / MUESTRAS;
}

void setup() {
  Serial.begin(115200);
  analogReadResolution(12);
  analogSetPinAttenuation(PIN_LDR, ADC_11db);   // rango completo 0–3.3 V
  delay(500);
  Serial.println();
  Serial.println("=== Prueba de LDR en D34 ===");
  Serial.println("Tapa y destapa la LDR. 'r' + Intro reinicia min/max.");
}

void loop() {
  if (Serial.available()) {
    char c = Serial.read();
    if (c == 'r' || c == 'R') { minimo = 4095; maximo = 0; Serial.println("-- min/max reiniciados --"); }
  }

  int crudo = leerPromedio();
  int mv    = analogReadMilliVolts(PIN_LDR);   // voltaje calibrado de fábrica
  if (crudo < minimo) minimo = crudo;
  if (crudo > maximo) maximo = crudo;

  // Resistencia estimada de la LDR con el divisor: R_ldr = R_fija * (Vref - V) / V
  float v = mv / 1000.0f;
  String rLdr;
  if (mv < 30)                 rLdr = "abierta (no llega voltaje)";
  else if (mv > 3250)          rLdr = "~0 (D34 pegado a 3V3)";
  else                         rLdr = String(R_FIJA * (VREF - v) / v / 1000.0f, 1) + " kOhm";

  int porcentaje = constrain(map(crudo, 200, 3800, 0, 100), 0, 100);   // misma fórmula del firmware

  // Barra visual de 20 caracteres
  String barra = "";
  int llenos = crudo * 20 / 4095;
  for (int i = 0; i < 20; i++) barra += (i < llenos) ? '#' : '.';

  Serial.printf("ADC=%4d  V=%4d mV  LDR=%-26s luz=%3d%%  [%s]  min=%4d max=%4d\n",
                crudo, mv, rLdr.c_str(), porcentaje, barra.c_str(), minimo, maximo);
  delay(500);
}
