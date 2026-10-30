// =====================================================================
//  ClimaPlus – Configuración privada del ESP32 (PLANTILLA)
//
//  1. Copia este archivo como "secrets.h" en esta misma carpeta.
//  2. Llena tus datos reales en secrets.h.
//  secrets.h está en .gitignore: NUNCA se sube a GitHub.
// =====================================================================
#pragma once

// ---------- Red WiFi (2.4 GHz; el ESP32 no se conecta a redes de 5 GHz) ----------
#define WIFI_SSID     "NOMBRE_DE_TU_RED"
#define WIFI_PASSWORD "CLAVE_DE_TU_RED"

// ---------- API de ClimaPlus ----------
// IP de la PC donde corre la API (ver con "ipconfig" → Dirección IPv4). No uses "localhost":
// para el ESP32, localhost es el propio ESP32.
#define API_URL "http://192.168.1.50:5208/api/lecturas"

// API Key que muestra la pantalla "Mis dispositivos" al registrar el dispositivo (una sola vez).
#define DEVICE_KEY "cpk_PEGA_AQUI_LA_CLAVE_DEL_DISPOSITIVO"

// ---------- Opcional ----------
// Descomenta para el simulador Wokwi: usa un DHT22 en lugar del BME280.
// #define MODO_SIMULACION
