// =====================================================================
//  ClimaPlus – Diagnóstico del sensor de lluvia MH-RD con módulo de relé (Fase 7)
//  Programa independiente: sin WiFi, sin LCD, sin API. No modifica el firmware principal.
//
//  Lee los CONTACTOS SECOS del relé (NO / COM / NC) como un interruptor:
//
//      Módulo de relé                     ESP32 DevKit
//      COM  ───────────────────────────── GND
//      NO o NC (el que se CIERRA mojado) ─ D27  (INPUT_PULLUP interno)
//
//  ⚠ Antes de conectar, sigue docs/hardware/diagnostico-lluvia-ldr.md (pasos L-1 a L-6):
//    - Alimenta el módulo con SU voltaje (lo indica el relé: SRD-05VDC = 5 V, SRD-12VDC = 12 V), nunca desde un GPIO.
//    - Comprueba con el multímetro que COM, NO y NC NO tienen continuidad con VCC ni con GND del módulo.
//    - NO conectes VCC, DO, AO ni ningún otro pin del módulo al ESP32: solo COM y un contacto.
//
//  Monitor serie a 115200. Moja la placa MH-RD y luego sécala: el estado debe cambiar.
//  LED rojo = contacto cerrado · LED verde = contacto abierto (mismos pines del firmware).
//  Escribe "r" + Intro para reiniciar los contadores.
// =====================================================================
#include <Arduino.h>

const uint8_t PIN_CONTACTO     = 27;   // mismo GPIO que PIN_LLUVIA en climaplus_esp32_lcd.ino
const uint8_t PIN_LED_ROJO     = 25;
const uint8_t PIN_LED_VERDE    = 26;
const uint8_t PIN_LED_AMARILLO = 33;
const uint8_t PIN_BUZZER       = 32;   // se deja apagado durante la prueba

const unsigned long ANTIRREBOTE_MS = 50;    // un cambio cuenta si dura al menos 50 ms
const unsigned long RESUMEN_MS     = 2000;  // línea de estado cada 2 s

int           estadoEstable      = HIGH;   // HIGH = contacto abierto (pull-up), LOW = cerrado a GND
int           ultimoCrudo        = HIGH;
unsigned long ultimoCambioCrudo  = 0;
unsigned long inicioEstado       = 0;
unsigned long ultimoResumen      = 0;
unsigned long cambiosEstables    = 0;
unsigned long rebotesVentana     = 0;   // cambios crudos en la ventana del resumen

void mostrarLeds(int estado) {
  digitalWrite(PIN_LED_ROJO, estado == LOW ? HIGH : LOW);
  digitalWrite(PIN_LED_VERDE, estado == HIGH ? HIGH : LOW);
  digitalWrite(PIN_LED_AMARILLO, LOW);
}

const char *texto(int estado) {
  return estado == LOW ? "CERRADO (D27=LOW)" : "ABIERTO (D27=HIGH)";
}

void setup() {
  Serial.begin(115200);
  pinMode(PIN_CONTACTO, INPUT_PULLUP);
  pinMode(PIN_LED_ROJO, OUTPUT);
  pinMode(PIN_LED_VERDE, OUTPUT);
  pinMode(PIN_LED_AMARILLO, OUTPUT);
  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_BUZZER, LOW);

  delay(500);
  estadoEstable = ultimoCrudo = digitalRead(PIN_CONTACTO);
  inicioEstado = ultimoCambioCrudo = ultimoResumen = millis();
  mostrarLeds(estadoEstable);

  Serial.println();
  Serial.println("=== Diagnostico de lluvia MH-RD (contactos del rele en D27) ===");
  Serial.println("COM -> GND del ESP32; NO o NC -> D27. Moja y seca la placa sensora.");
  Serial.println("Nota: sin nada conectado D27 tambien lee ABIERTO (pull-up interno).");
  Serial.printf("Estado inicial: contacto %s\n", texto(estadoEstable));
}

void loop() {
  if (Serial.available()) {
    char c = Serial.read();
    if (c == 'r' || c == 'R') {
      cambiosEstables = 0;
      rebotesVentana = 0;
      Serial.println("-- contadores reiniciados --");
    }
  }

  int crudo = digitalRead(PIN_CONTACTO);
  if (crudo != ultimoCrudo) {
    ultimoCrudo = crudo;
    ultimoCambioCrudo = millis();
    rebotesVentana++;
  }

  // Cambio estable: el valor crudo se mantuvo distinto del estado actual durante ANTIRREBOTE_MS.
  if (crudo != estadoEstable && millis() - ultimoCambioCrudo >= ANTIRREBOTE_MS) {
    float duracion = (millis() - inicioEstado) / 1000.0f;
    estadoEstable = crudo;
    inicioEstado = millis();
    cambiosEstables++;
    mostrarLeds(estadoEstable);
    Serial.printf("[%8.1f s] CAMBIO -> contacto %s (el estado anterior duro %.1f s, cambios=%lu)\n",
                  millis() / 1000.0f, texto(estadoEstable), duracion, cambiosEstables);
  }

  if (millis() - ultimoResumen >= RESUMEN_MS) {
    ultimoResumen = millis();
    Serial.printf("[%8.1f s] contacto %s desde hace %.1f s | cambios=%lu",
                  millis() / 1000.0f, texto(estadoEstable), (millis() - inicioEstado) / 1000.0f, cambiosEstables);
    // Más de 4 cambios crudos en 2 s indica rebote: placa casi en el umbral del potenciómetro o contacto flojo.
    if (rebotesVentana > 4) Serial.printf("  AVISO inestable: %lu cambios en 2 s", rebotesVentana);
    Serial.println();
    rebotesVentana = 0;
  }
}
