# ESP32: validar el certificado HTTPS de ClimaPlus (propuesta, requiere autorización)

**Estado:** **aplicado** en `hardware/esp32/climaplus_esp32_lcd/` (`ca_raiz.h` y `http.begin(API_URL, CA_RAIZ)` cuando
`API_URL` empieza por `https://`). Compila con el núcleo esp32 3.0.7 (82 % de flash, 14 % de RAM). Falta la prueba
con la placa contra el servidor real.

## Diagnóstico

El firmware llama a `http.begin(API_URL)` sin certificado raíz. En el núcleo ESP32 3.0.7, `HTTPClient`
(`libraries/HTTPClient/src/HTTPClient.cpp`, clase `TLSTraits`) hace `setInsecure()` cuando no recibe una CA:

- con `API_URL` en `https://…` la conexión **se cifra pero no se verifica el servidor**: cualquiera en la misma red WiFi
  podría suplantar a ClimaPlus y recibir la `X-Device-Key`;
- con `http://…` Nginx responde 301 y el ESP32 no repite el POST: la lectura se pierde.

## Cambio propuesto (dos líneas de lógica y un archivo nuevo)

1. Copiar `docs/hardware/esp32-https/ca_raiz.h` a `hardware/esp32/climaplus_esp32_lcd/ca_raiz.h`. Contiene los
   certificados raíz **públicos** de Let's Encrypt (no son secretos):
   - ISRG Root X1 (RSA, vence 2035) – SHA-256 `96:BC:EC:06:…:BD:DF:08:C6`
   - ISRG Root X2 (ECDSA, vence 2040) – SHA-256 `69:72:9B:8E:…:3C:CB:14:70`
   Se incluyen las dos porque certbot emite certificados ECDSA por defecto y la cadena puede terminar en cualquiera.
   Se tomaron del almacén de confianza de Ubuntu y sus huellas coinciden con las publicadas en
   <https://letsencrypt.org/certificates/>.
2. En el `.ino`:

```diff
 #include "secrets.h"   // Copia secrets.example.h como secrets.h y llena tus datos.
+#include "ca_raiz.h"   // certificados raíz de Let's Encrypt: el ESP32 valida el servidor HTTPS
 ...
   HTTPClient http;
   http.setTimeout(TIMEOUT_HTTP_MS);
-  http.begin(API_URL);
+  // HTTPS: valida el certificado del servidor contra CA_RAIZ (nunca setInsecure). HTTP solo para pruebas en la red local.
+  if (strncmp(API_URL, "https://", 8) == 0) {
+    http.begin(API_URL, CA_RAIZ);
+  } else {
+    http.begin(API_URL);
+  }
```

3. En `secrets.h` (no se sube a Git): `#define API_URL "https://<DOMINIO>/api/lecturas"`.

Comprobado: el firmware con este cambio **compila** con arduino-cli y el núcleo esp32 3.0.7 (ESP32 Dev Module):
82 % de flash y 14 % de RAM, igual que antes. **Falta probarlo en el hardware** contra el servidor real.

## Notas

- `begin(url, CA)` usa `setCACert`: si el certificado del servidor no encadena con X1/X2, la conexión falla (código
  HTTP negativo en el monitor serie y LED amarillo). No hay ninguna opción para "aceptar cualquier certificado".
- No hace falta NTP: este núcleo compila mbedTLS sin `MBEDTLS_HAVE_TIME_DATE` (no compara fechas de vigencia); la
  firma y el nombre del servidor sí se verifican. La renovación de Let's Encrypt (cada ~60 días) no requiere tocar el
  ESP32 mientras la raíz sea la misma.
- La `DEVICE_KEY` no cambia: la base restaurada en Azure conserva el hash de la clave.
