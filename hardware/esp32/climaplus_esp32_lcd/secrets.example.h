// =====================================================================
//  ClimaPlus – Configuración privada del ESP32 (PLANTILLA) · versión LCD 16x2 + DHT
//
//  1. Copia este archivo como "secrets.h" en esta misma carpeta (o crea la pestaña secrets.h en el IDE).
//  2. Llena tus datos reales en secrets.h.
//  secrets.h está en .gitignore: NUNCA se sube a GitHub.
// =====================================================================
#pragma once

// ---------- Red WiFi (2.4 GHz; el ESP32 no se conecta a redes de 5 GHz) ----------
#define WIFI_SSID     "NOMBRE_DE_TU_RED"
#define WIFI_PASSWORD "CONTRASENA_DE_TU_RED"

// ---------- API de ClimaPlus ----------
// IP de tu PC (ipconfig → Dirección IPv4). No uses "localhost".

//   En modo desarrollo:  http://TU-IP:5208/api/lecturas
#define API_URL "http://192.168.1.50:8080/api/lecturas"

// API Key que muestra "Mis dispositivos" al registrar el dispositivo (una sola vez).
#define DEVICE_KEY "cpk_PEGA_AQUI_TU_API_KEY"

// ---------- Sensor DHT ----------
// DHT11 = carcasa azul (0–50 °C, 20–90 %).  DHT22 = carcasa blanca (−40–80 °C, 0–100 %).
#define TIPO_DHT DHT11
