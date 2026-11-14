// =====================================================================
//  ClimaPlus – Caja (mockup) para el hardware del ESP32
//  2 protoboards de 830 puntos + ESP32 DOIT DEVKIT V1 + LCD 16x2 I2C
//  + DHT11/22 + LDR + sensor de lluvia LM393 + 3 LED + buzzer + botón
//  + torre meteorológica (mástil hueco para los cables, bandeja para la placa
//    de lluvia, garita ventilada para el DHT y la LDR en la punta)
//
//  Uso en https://ochafik.com/openscad2/ :
//    - Pega el código y pulsa Render (F6) o Preview (F5).
//    - Cambia "vista" en el panel Customizer:
//        ensamblado   → caja con todos los componentes (tapa transparente)
//        explosionado → la tapa levantada para ver el interior
//        base / tapa  → una sola pieza, lista para exportar a STL
//        impresion    → base y tapa juntas en posición de impresión
//        torre        → las 3 piezas de la torre listas para imprimir (sin soportes)
//    - Mide tus protoboards con una regla y ajusta "pb_largo/pb_ancho/pb_alto"
//      si no son de 830 puntos (las de 400 puntos miden 82.5 x 54.6 mm).
//
//  Universidad Mariano Gálvez · Cobán · Desarrollo Web · Proyecto ClimaPlus
// =====================================================================

/* [Vista] */
vista = "ensamblado"; // [ensamblado, explosionado, base, tapa, impresion, torre]
// Altura a la que se levanta la tapa en la vista "explosionado" (mm)
separacion_explosion = 70;

/* [Protoboards (2 iguales, lado a lado)] */
// Largo de cada protoboard (830 puntos = 165.1 mm)
pb_largo = 165.1;
// Ancho de cada protoboard (54.6 mm)
pb_ancho = 54.6;
// Alto incluyendo el adhesivo de la base
pb_alto = 9.5;
// Espacio entre las dos protoboards (0 = unidas por sus encastres)
pb_separacion = 0;

/* [Caja] */
// Espacio libre alrededor de las protoboards
holgura = 2;
pared = 2.4;
piso = 2.4;
// Altura libre desde el piso hasta la tapa (cables, DHT y LCD)
alto_interior = 50;
tapa_grosor = 2.4;
// Reborde de la tapa que entra en la caja
tapa_reborde_alto = 4;
tapa_reborde_grosor = 1.6;
// Juego entre el reborde y la pared (súbelo si la tapa entra muy justa)
ajuste = 0.3;
radio_esquina = 3;
// Profundidad del agujero para tornillo M3 autorroscante en las columnas
prof_tornillo = 12;

/* [ESP32 DOIT DEVKIT V1] */
// true = el ESP32 va montado sobre la unión de las dos protoboards (no cabe en una sola)
esp_en_union = true;
esp_largo = 52;
esp_ancho = 28.5;
// Distancia del borde de la protoboard al ESP32 (lado del puerto USB)
esp_margen = 2;
// Altura de la placa del ESP32 sobre la protoboard (separador de los pines)
esp_elevacion = 2.5;
usb_hueco_ancho = 13;
usb_hueco_alto = 9;

/* [Tapa: perforaciones] */
diametro_led = 5.2;
// Agujero del botón (7 = botón táctil con extensión; 12 = pulsador de panel)
diametro_boton = 7;
diametro_ldr = 5.5;
// Ventana del LCD 16x2 (la pantalla con marco mide 71.2 x 24.2 mm)
ventana_lcd = [72.5, 25.5];
// Separación de los agujeros de montaje del LCD (75 x 31 mm)
agujeros_lcd = [75, 31];
// Profundidad del texto grabado
grabado = 0.6;

/* [Torre meteorológica] */
// true = DHT, LDR y lluvia van en la torre; false = DHT y lluvia en la pared derecha
usar_torre = true;
// Altura del mástil sobre la tapa (sin la punta)
torre_alto = 80;
torre_d = 18;
// Hueco interior para los cables (DHT 3 + LDR 2 + lluvia 2)
torre_d_int = 12;
brida_d = 40;
brida_t = 3;
// Placa de lluvia YL-83 / FC-37
placa_lluvia = [54, 40];
// Altura (sobre la tapa) del encaje de la bandeja de lluvia
alto_bandeja = 55;
// Altura (sobre la tapa) del encaje de la garita del DHT
alto_garita = 45;
// Interior de la garita: profundidad, ancho, alto (cabe el módulo DHT11/DHT22)
garita_int = [14, 17, 32];
// Paredes de las piezas de la torre
torre_pared = 1.6;
// Juego de los encajes (súbelo a 0.3 si entran muy justos)
juego = 0.2;

$fn = 48;

// ---------------------------------------------------------------------- Medidas derivadas
in_x  = pb_largo + 2 * holgura;
in_y  = 2 * pb_ancho + pb_separacion + 2 * holgura;
out_x = in_x + 2 * pared;
out_y = in_y + 2 * pared;
caja_alto = piso + alto_interior;            // la tapa se apoya sobre esta altura
tapa_top  = caja_alto + tapa_grosor;

bb_top = piso + pb_alto;
bb1 = [pared + holgura, pared + holgura];
bb2 = [pared + holgura, pared + holgura + pb_ancho + pb_separacion];

esp_x = bb1[0] + esp_margen;
esp_y = esp_en_union
        ? bb2[1] - pb_separacion / 2           // centrado en la unión de las dos protoboards
        : bb1[1] + pb_ancho / 2;               // centrado en el canal de la protoboard 1
esp_z = bb_top + esp_elevacion;
usb_z = esp_z + 1.6 + 1.4;                   // centro del conector USB (micro-USB o USB-C)

// Columnas de las esquinas (tornillos de la tapa)
r_col = 4.5;
c_off = 1;
esquinas = [[-c_off, -c_off], [out_x + c_off, -c_off],
            [-c_off, out_y + c_off], [out_x + c_off, out_y + c_off]];

// Posiciones en la tapa (coordenadas de la caja)
lcd_c    = [out_x - pared - 12 - 40, out_y - pared - 14 - 18];
poste_lcd = 7 - tapa_grosor;                 // el marco del LCD queda al ras de la tapa
leds     = [[pared + 16, out_y - 22], [pared + 34, out_y - 22], [pared + 52, out_y - 22]];
led_col  = ["red", "lime", "yellow"];
led_txt  = ["ALERTA", "OK", "ERROR"];
boton_c  = [pared + 34, out_y - 50];
buzzer_c = [pared + 20, pared + 34];
ldr_c    = [pared + 50, pared + 34];
torre_c  = [pared + 68, pared + 34];        // centro del mástil sobre la tapa

// DHT y lluvia en la pared +X
dht_y = bb2[1] + pb_ancho / 2;
dht_z = bb_top + 22;
lluvia_y = bb1[1] + pb_ancho / 2;
lluvia_z = bb_top + 6;

// ---------------------------------------------------------------------- Utilidades
module rrect(x, y, r) {
  translate([r, r]) offset(r = r) square([x - 2 * r, y - 2 * r]);
}

module perfil_exterior() {
  union() {
    rrect(out_x, out_y, radio_esquina);
    for (p = esquinas) translate(p) circle(r = r_col);
  }
}

// Ranura redondeada que atraviesa una pared perpendicular a X
module ranura_x(ancho, alto, largo) {
  r = min(ancho, alto) / 2;
  rotate([0, 90, 0]) linear_extrude(largo)
    hull() for (dy = [-(ancho / 2 - r), ancho / 2 - r], dz = [-(alto / 2 - r), alto / 2 - r])
      translate([-dz, dy]) circle(r = r);
}

// Ranura redondeada que atraviesa una pared perpendicular a Y
module ranura_y(ancho, alto, largo) {
  r = min(ancho, alto) / 2;
  rotate([-90, 0, 0]) linear_extrude(largo)
    hull() for (dx = [-(ancho / 2 - r), ancho / 2 - r], dz = [-(alto / 2 - r), alto / 2 - r])
      translate([dx, -dz]) circle(r = r);
}

module texto(t, tam, estilo = "Bold") {
  text(t, size = tam, font = str("Liberation Sans:style=", estilo), halign = "center", valign = "center");
}

// Texto grabado en la cara +X (se lee desde fuera)
module texto_pared_px(t, y, z, tam) {
  translate([out_x - grabado, y, z]) rotate([90, 0, 90]) linear_extrude(2) texto(t, tam);
}
// Texto grabado en la cara -X
module texto_pared_mx(t, y, z, tam) {
  translate([grabado, y, z]) rotate([90, 0, -90]) linear_extrude(2) texto(t, tam);
}

// ---------------------------------------------------------------------- Base
module base() {
  difference() {
    linear_extrude(caja_alto) perfil_exterior();

    // Cavidad interior
    translate([pared, pared, piso]) cube([in_x, in_y, caja_alto]);

    // Tornillos M3 de la tapa
    for (p = esquinas) translate([p[0], p[1], caja_alto - prof_tornillo]) cylinder(d = 2.6, h = prof_tornillo + 1);

    // Pared -X: puerto USB del ESP32 (sigue la posición esp_y)
    translate([-1, esp_y, usb_z]) ranura_x(usb_hueco_ancho, usb_hueco_alto, pared + 2);
    texto_pared_mx("USB", esp_y, usb_z + usb_hueco_alto / 2 + 5, 5);

    if (!usar_torre) {
    // Pared +X: ventana del DHT (queda afuera, lejos del calor del ESP32)
    translate([out_x - pared - 1, dht_y, dht_z]) ranura_x(17, 14, pared + 2);
    texto_pared_px("DHT", dht_y, dht_z + 12, 5);
    // Ranuras de ventilación junto al DHT
    for (i = [-2:2]) if (i != 0)
      translate([out_x - pared - 1, dht_y + i * 9 + (i > 0 ? 6 : -6), dht_z]) ranura_x(3, 14, pared + 2);

    // Pared +X: salida del cable del sensor de lluvia
    translate([out_x - pared - 1, lluvia_y, lluvia_z]) ranura_x(10, 6, pared + 2);
    texto_pared_px("LLUVIA", lluvia_y, lluvia_z + 8, 4);
    }

    // Pared frontal (-Y): nombre del proyecto
    translate([out_x / 2, grabado, piso + 15]) rotate([90, 0, 0]) linear_extrude(2) texto("ClimaPlus", 8);
    translate([out_x / 2, grabado, piso + 6]) rotate([90, 0, 0]) linear_extrude(2) texto("ESP32 · UMG Cobán", 4, "Regular");

    // Paredes ±Y: ventilación
    for (i = [0:12]) {
      x = 40 + i * 9;
      translate([x, -1, bb_top + 24]) ranura_y(3, 16, pared + 2);
      translate([x, out_y - pared - 1, bb_top + 24]) ranura_y(3, 16, pared + 2);
    }
  }

  // Topes en el piso para que las protoboards no se deslicen
  // (a lo largo de las paredes, en el espacio de holgura)
  color("gainsboro") {
    for (x = [pared + in_x * 0.25, pared + in_x * 0.75]) {
      translate([x - 10, pared, piso]) cube([20, holgura - 0.2, 3]);
      translate([x - 10, out_y - pared - holgura + 0.2, piso]) cube([20, holgura - 0.2, 3]);
    }
    for (y = [bb1[1] + pb_ancho / 2, bb2[1] + pb_ancho / 2]) {
      translate([pared, y - 8, piso]) cube([holgura - 0.2, 16, 3]);
      translate([out_x - pared - holgura + 0.2, y - 8, piso]) cube([holgura - 0.2, 16, 3]);
    }
  }
}

// ---------------------------------------------------------------------- Tapa
module tapa() {
  difference() {
    union() {
      translate([0, 0, caja_alto]) linear_extrude(tapa_grosor) perfil_exterior();

      // Reborde que entra en la caja
      translate([pared + ajuste, pared + ajuste, caja_alto - tapa_reborde_alto])
        difference() {
          cube([in_x - 2 * ajuste, in_y - 2 * ajuste, tapa_reborde_alto]);
          translate([tapa_reborde_grosor, tapa_reborde_grosor, -1])
            cube([in_x - 2 * ajuste - 2 * tapa_reborde_grosor, in_y - 2 * ajuste - 2 * tapa_reborde_grosor, tapa_reborde_alto + 2]);
        }

      // Postes del LCD
      for (dx = [-1, 1], dy = [-1, 1])
        translate([lcd_c[0] + dx * agujeros_lcd[0] / 2, lcd_c[1] + dy * agujeros_lcd[1] / 2, caja_alto - poste_lcd])
          cylinder(d = 6, h = poste_lcd + 0.01);

      // Aro de soporte para el buzzer
      translate([buzzer_c[0], buzzer_c[1], caja_alto - 3]) difference() {
        cylinder(d = 16, h = 3.01);
        translate([0, 0, -1]) cylinder(d = 12.4, h = 5);
      }
    }

    // Ventana del LCD
    translate([lcd_c[0] - ventana_lcd[0] / 2, lcd_c[1] - ventana_lcd[1] / 2, caja_alto - 1])
      cube([ventana_lcd[0], ventana_lcd[1], tapa_grosor + 2]);
    // Agujeros para tornillos M3 del LCD
    for (dx = [-1, 1], dy = [-1, 1])
      translate([lcd_c[0] + dx * agujeros_lcd[0] / 2, lcd_c[1] + dy * agujeros_lcd[1] / 2, caja_alto - poste_lcd - 1])
        cylinder(d = 2.5, h = poste_lcd + 1 + tapa_grosor - 0.8);

    // LED
    for (i = [0:2]) {
      translate([leds[i][0], leds[i][1], caja_alto - 1]) cylinder(d = diametro_led, h = tapa_grosor + 2);
      translate([leds[i][0], leds[i][1] - 7, tapa_top - grabado]) linear_extrude(1) texto(led_txt[i], 3);
    }

    // Botón
    translate([boton_c[0], boton_c[1], caja_alto - 1]) cylinder(d = diametro_boton, h = tapa_grosor + 2);
    translate([boton_c[0], boton_c[1] - diametro_boton / 2 - 4, tapa_top - grabado]) linear_extrude(1) texto("BOTON", 3);

    // Rejilla del buzzer
    translate([buzzer_c[0], buzzer_c[1], caja_alto - 1]) {
      cylinder(d = 2, h = tapa_grosor + 2);
      for (a = [0:60:300]) rotate(a) translate([3.6, 0, 0]) cylinder(d = 2, h = tapa_grosor + 2);
    }
    translate([buzzer_c[0], buzzer_c[1] - 11, tapa_top - grabado]) linear_extrude(1) texto("BUZZER", 3);

    if (usar_torre) {
      // Torre: paso de cables y 3 tornillos M3 de la brida
      translate([torre_c[0], torre_c[1], caja_alto - 1]) {
        cylinder(d = torre_d_int, h = tapa_grosor + 2);
        for (a = [90, 210, 330]) rotate(a) translate([brida_d / 2 - 5, 0, 0]) cylinder(d = 3.4, h = tapa_grosor + 2);
      }
    } else {
      // LDR (luz)
      translate([ldr_c[0], ldr_c[1], caja_alto - 1]) cylinder(d = diametro_ldr, h = tapa_grosor + 2);
      translate([ldr_c[0], ldr_c[1] - 9, tapa_top - grabado]) linear_extrude(1) texto("LUZ", 3);
    }

    // Tornillos de la tapa (avellanados)
    for (p = esquinas) translate([p[0], p[1], caja_alto - 1]) {
      cylinder(d = 3.4, h = tapa_grosor + 2);
      translate([0, 0, 1 + tapa_grosor - 1.6]) cylinder(d1 = 3.4, d2 = 6.6, h = 1.61);
    }

    // Título grabado arriba del LCD
    translate([lcd_c[0], lcd_c[1] + 22, tapa_top - grabado]) linear_extrude(1) texto("CLIMAPLUS", 5);
  }
}

// ---------------------------------------------------------------------- Componentes (solo para ver)
module protoboard(p) {
  translate([p[0], p[1], piso]) {
    color("white") difference() {
      cube([pb_largo, pb_ancho, pb_alto]);
      translate([3, pb_ancho / 2 - 1.5, pb_alto - 2]) cube([pb_largo - 6, 3, 3]);
    }
    // Rieles de alimentación
    for (y = [2.5, pb_ancho - 3.5]) {
      color("red")  translate([6, y + 0.0, pb_alto]) cube([pb_largo - 12, 0.4, 0.05]);
      color("blue") translate([6, y + 1.0, pb_alto]) cube([pb_largo - 12, 0.4, 0.05]);
    }
    // Agujeros (sugeridos)
    color("dimgray") for (x = [8:2.54:pb_largo - 8], y = [9:2.54:pb_ancho / 2 - 3], s = [0, 1])
      translate([x, s ? pb_ancho - y : y, pb_alto - 0.01]) cube([0.9, 0.9, 0.05], center = true);
  }
}

module esp32() {
  translate([esp_x, esp_y - esp_ancho / 2, esp_z]) {
    color("midnightblue") cube([esp_largo, esp_ancho, 1.6]);
    color("silver") translate([esp_largo - 19, (esp_ancho - 18) / 2, 1.6]) cube([18, 18, 3.2]);
    color("black") translate([esp_largo - 6, (esp_ancho - 16) / 2, 1.6]) cube([6, 16, 0.8]);   // antena
    color("silver") translate([-1, esp_ancho / 2 - 4, 1.6]) cube([6, 8, 2.8]);                  // micro-USB
    color("gold") for (x = [3:2.54:esp_largo - 3], y = [1.3, esp_ancho - 1.3])
      translate([x, y, -esp_elevacion]) cylinder(d = 0.8, h = esp_elevacion);
  }
}

module lcd() {
  z = caja_alto - poste_lcd;   // cara frontal del PCB
  translate([lcd_c[0], lcd_c[1], 0]) {
    color("green") translate([-40, -18, z - 1.6]) cube([80, 36, 1.6]);
    color("black") translate([-35.6, -12.1, z]) cube([71.2, 24.2, 7 - 0.3]);
    color("deepskyblue") translate([-32.25, -7.2, z + 6.7]) cube([64.5, 14.4, 0.3]);
    color("dimgray") translate([-21, -9.5, z - 1.6 - 12]) cube([42, 19, 12]);   // módulo I2C
  }
}

module led(p, c) {
  translate([p[0], p[1], caja_alto - 1]) {
    color(c) cylinder(d = 5.8, h = 1);
    color(c, 0.85) translate([0, 0, 1]) { cylinder(d = 5, h = 5.6); translate([0, 0, 5.6]) sphere(d = 5); }
  }
}

// Componentes montados en la tapa (se mueven con ella en la vista explosionada)
module componentes_tapa() {
  lcd();
  for (i = [0:2]) led(leds[i], led_col[i]);

  // Buzzer
  color("black") translate([buzzer_c[0], buzzer_c[1], caja_alto - 9.5]) cylinder(d = 12, h = 9.5);
  // Botón (táctil 12x12 con capuchón)
  translate([boton_c[0], boton_c[1], caja_alto - 6]) {
    color("dimgray") translate([-6, -6, 0]) cube([12, 12, 3.5]);
    color("black") cylinder(d = diametro_boton - 0.8, h = 6 + tapa_grosor + 1);
  }
  // LDR (en la tapa solo si no hay torre)
  if (!usar_torre) translate([ldr_c[0], ldr_c[1], caja_alto + tapa_grosor - 2]) {
    color("orange") cylinder(d = 5, h = 2);
    color("silver") for (s = [-1, 1]) translate([s * 1.3, 0, -12]) cylinder(d = 0.5, h = 12);
  }
}

// Componentes montados en la base
module componentes_base() {
  protoboard(bb1);
  protoboard(bb2);
  esp32();
  if (!usar_torre) {
  // DHT11 en su módulo, asomado por la ventana de la pared +X
  translate([out_x - pared - 8, dht_y, dht_z]) {
    color("royalblue") translate([2, -7.75, -6]) cube([6 + pared, 15.5, 12]);
    color("darkgreen") translate([0, -14, -9]) cube([1.6, 28, 18]);
  }
  // Módulo LM393 del sensor de lluvia (dentro)
  color("darkblue") translate([out_x - pared - 36, lluvia_y - 8, bb_top + 1]) cube([30, 16, 1.6]);
  // Placa de lluvia (afuera de la caja)
  translate([out_x + 25, lluvia_y - 20, 0]) {
    color("darkred") cube([54, 40, 1.6]);
    color("gold") for (i = [0:7]) translate([4, 4 + i * 4.4, 1.6]) cube([46, 1.2, 0.05]);
  }
  // Cables hacia la placa de lluvia
  for (s = [-1, 1]) color(s < 0 ? "red" : "black")
    hull() {
      translate([out_x - pared - 6, lluvia_y + s * 1.5, lluvia_z]) sphere(d = 1.4);
      translate([out_x + 25, lluvia_y + s * 1.5, 1.6]) sphere(d = 1.4);
    }
  } else {
    // Módulo LM393 del sensor de lluvia (dentro de la caja)
    color("darkblue") translate([out_x - pared - 36, lluvia_y - 8, bb_top + 1]) cube([30, 16, 1.6]);
  }
}


// ---------------------------------------------------------------------- Torre meteorológica
// Coordenadas locales: origen en el centro de la brida, sobre la tapa.
// Bandeja de lluvia hacia +X, garita del DHT hacia -X, LDR en la punta.
// Se imprime en 3 piezas sin soportes: mástil, bandeja (plana) y garita (de pie).
// Las piezas se encajan bajando su lengüeta en la ranura del mástil (+ una gota de pegamento).

enc_r0   = torre_d / 2 - 1;          // inicio del encaje (dentro del tubo)
enc_len  = 9;                         // cuánto sobresale el encaje
enc_w    = 16;
enc_h    = 14;
ran      = [4.4, 10.4];               // ranura en T: grosor radial, ancho
ran_x0   = enc_r0 + 3;
abertura = 5.4;
enc_cara = enc_r0 + enc_len;          // cara exterior del encaje
pieza_x0 = enc_cara + 0.4;            // donde empieza la bandeja / garita

bandeja = [placa_lluvia[0] + 1 + 2 * torre_pared, placa_lluvia[1] + 1 + 2 * torre_pared, 1.2 + 3.5];
garita  = [garita_int[0] + 2 * torre_pared, garita_int[1] + 2 * torre_pared, garita_int[2] + torre_pared];

// Encaje en T del mástil (hacia +X), con la parte de arriba en ztop
module encaje(ztop) {
  translate([0, 0, ztop - enc_h]) difference() {
    translate([enc_r0, -enc_w / 2, 0]) cube([enc_len, enc_w, enc_h]);
    translate([ran_x0, -ran[1] / 2, 2]) cube([ran[0], ran[1], enc_h]);
    translate([ran_x0 + ran[0] - 0.1, -abertura / 2, 2]) cube([enc_cara - ran_x0 - ran[0] + 1, abertura, enc_h]);
  }
}

// Lengüeta en T que entra en el encaje (forma parte de la bandeja o la garita)
module lengueta(ztop) {
  h = enc_h - 2;
  translate([0, 0, ztop - h]) {
    translate([ran_x0 + juego, -(ran[1] / 2 - juego), 0]) cube([ran[0] - 2 * juego, ran[1] - 2 * juego, h]);
    translate([ran_x0 + ran[0] - juego - 0.01, -(abertura / 2 - juego), 0])
      cube([pieza_x0 + torre_pared - (ran_x0 + ran[0] - juego), abertura - 2 * juego, h]);
  }
}

module mastil() {
  difference() {
    union() {
      cylinder(d = brida_d, h = brida_t);
      cylinder(d = torre_d, h = torre_alto);
      // Punta con la LDR mirando al cielo
      translate([0, 0, torre_alto]) cylinder(d1 = torre_d, d2 = 9, h = 9);
      // Aros decorativos de "estación"
      for (z = [torre_alto * 0.35, torre_alto - 6]) translate([0, 0, z]) cylinder(d = torre_d + 3, h = 2);
      encaje(alto_bandeja);
      mirror([1, 0, 0]) encaje(alto_garita);
    }
    // Hueco para los cables
    translate([0, 0, -1]) cylinder(d = torre_d_int, h = torre_alto + 0.5);
    // Agujero de la LDR en la punta
    translate([0, 0, torre_alto - 1]) cylinder(d = diametro_ldr, h = 12);
    // Tornillos M3 de la brida (avellanados por arriba)
    for (a = [90, 210, 330]) rotate(a) translate([brida_d / 2 - 5, 0, -1]) {
      cylinder(d = 3.4, h = brida_t + 2);
      translate([0, 0, brida_t + 1 - 1.6]) cylinder(d1 = 3.4, d2 = 6.6, h = 1.61);
    }
    // Salidas de cables hacia la bandeja (arriba del encaje) y la garita (abajo)
    translate([0, 0, alto_bandeja + 5]) rotate([0, 90, 0]) cylinder(d = 6, h = torre_d);
    translate([0, 0, alto_garita - garita[2] - 4]) rotate([0, -90, 0]) cylinder(d = 6, h = torre_d);
  }
}

// Bandeja para la placa de lluvia (+X). El suelo es una rejilla: el agua escurre y gasta menos filamento.
module bandeja_lluvia() {
  zb = alto_bandeja - (enc_h - 2);
  union() {
    translate([pieza_x0, -bandeja[1] / 2, zb]) difference() {
      cube(bandeja);
      translate([torre_pared, torre_pared, 1.2]) cube([bandeja[0] - 2 * torre_pared, bandeja[1] - 2 * torre_pared, 10]);
      // Ventanas del suelo
      for (i = [0:2], j = [0:1])
        translate([torre_pared + 3 + i * (bandeja[0] - 2 * torre_pared - 6) / 3,
                   torre_pared + 3 + j * (bandeja[1] - 2 * torre_pared - 6) / 2, -1])
          cube([(bandeja[0] - 2 * torre_pared - 6) / 3 - 4, (bandeja[1] - 2 * torre_pared - 6) / 2 - 4, 3]);
      // Muesca para los pines de la placa (extremo exterior)
      translate([bandeja[0] - torre_pared - 1, bandeja[1] / 2 - 6, 1.2]) cube([torre_pared + 2, 12, 10]);
    }
    lengueta(alto_bandeja);
  }
}

// Garita ventilada para el DHT (-X): abierta por abajo, con persianas en 3 caras.
module garita_dht() {
  mirror([1, 0, 0]) union() {
    translate([pieza_x0, -garita[1] / 2, alto_garita - garita[2]]) difference() {
      cube(garita);
      translate([torre_pared, torre_pared, -1]) cube([garita_int[0], garita_int[1], garita_int[2] + 1]);
      for (z = [5:4.5:garita[2] - 5]) {
        // persianas laterales
        translate([3, -1, z]) cube([garita[0] - 6, garita[1] + 2, 2.2]);
        // persianas del frente
        translate([garita[0] - torre_pared - 1, 3, z]) cube([torre_pared + 2, garita[1] - 6, 2.2]);
      }
    }
    lengueta(alto_garita);
  }
}

module torre() {
  color("whitesmoke") mastil();
  color("lightgray") bandeja_lluvia();
  color("lightgray") garita_dht();
}

// Sensores montados en la torre (solo para ver)
module sensores_torre() {
  // Placa de lluvia en la bandeja
  translate([pieza_x0 + torre_pared + 0.5, -placa_lluvia[1] / 2, alto_bandeja - (enc_h - 2) + 1.2]) {
    color("darkred") cube([placa_lluvia[0], placa_lluvia[1], 1.6]);
    color("gold") for (i = [0:7]) translate([4, 3 + i * 4.6, 1.6]) cube([placa_lluvia[0] - 8, 1.2, 0.05]);
  }
  // DHT11 en su módulo dentro de la garita
  translate([-(pieza_x0 + torre_pared), 0, alto_garita - garita[2] / 2]) {
    color("darkgreen") translate([-2.6, -6.5, -14]) cube([1.6, 13, 28]);
    color("royalblue") translate([-2.6 - 5.5, -7.75, -6]) cube([5.5, 15.5, 12]);
  }
  // LDR en la punta
  translate([0, 0, torre_alto + 7]) color("orange") cylinder(d = 5, h = 2.2);
}

// Torre colocada sobre la tapa
module torre_en_tapa(con_sensores = true) {
  translate([torre_c[0], torre_c[1], tapa_top]) {
    torre();
    if (con_sensores) sensores_torre();
  }
}

// Las 3 piezas en posición de impresión
module torre_impresion() {
  mastil();
  translate([30 - pieza_x0, -(bandeja[1] / 2 + 30), -(alto_bandeja - (enc_h - 2))]) bandeja_lluvia();
  translate([-30 + pieza_x0, bandeja[1] / 2 + 10, -(alto_garita - garita[2])]) garita_dht();
}

// ---------------------------------------------------------------------- Vistas
if (vista == "ensamblado") {
  color("steelblue") base();
  componentes_base();
  componentes_tapa();
  %tapa();
  if (usar_torre) torre_en_tapa();                                   // tapa transparente en la vista previa (F5)
} else if (vista == "explosionado") {
  color("steelblue") base();
  componentes_base();
  translate([0, 0, separacion_explosion]) {
    color("lightsteelblue") tapa();
    componentes_tapa();
    if (usar_torre) translate([0, 0, separacion_explosion * 0.6]) torre_en_tapa();
  }
} else if (vista == "base") {
  base();
} else if (vista == "tapa") {
  translate([0, out_y, tapa_top]) rotate([180, 0, 0]) tapa();
} else if (vista == "torre") {
  torre_impresion();
} else if (vista == "impresion") {
  base();
  translate([out_x + 20, out_y, tapa_top]) rotate([180, 0, 0]) tapa();
}

// Medidas exteriores en la consola
echo(str("Caja: ", out_x, " x ", out_y, " x ", tapa_top, " mm (con columnas de esquina: ", out_x + 2 * (r_col + c_off), " x ", out_y + 2 * (r_col + c_off), ")"));
