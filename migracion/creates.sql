CREATE TABLE "accesorios_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio" text,
  "fecha_entrada" date,
  "recibido_por" text,
  "subcategoria" text,
  "marca" text,
  "modelo" text,
  "no_serie" text,
  "cantidad" integer,
  "tipo" text,
  "accesorios" text,
  "proveedor" text,
  "costo" numeric,
  "moneda" text,
  "disponible" boolean,
  "fecha_salida" date,
  "asignado_a" text,
  "destino_planta" text,
  "personal_it_que_asigna" text
);

CREATE TABLE "accesorios_nf_historial" (
  "id" integer PRIMARY KEY,
  "accesorio_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "auditoria_correctivos" (
  "id" bigint PRIMARY KEY,
  "registro_id" bigint,
  "fecha_cambio" timestamptz,
  "usuario" text,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "auditoria_ordenes_de_compra" (
  "id" integer PRIMARY KEY,
  "registro_id" integer,
  "fecha_cambio" timestamp,
  "usuario" text,
  "registro_anterior" text,
  "registro_nuevo" text
);

CREATE TABLE "auditoria_preventivos" (
  "id" bigint PRIMARY KEY,
  "registro_id" bigint,
  "fecha_cambio" timestamptz,
  "usuario" text,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "auditoria_registro_entradas_temporal" (
  "id" integer PRIMARY KEY,
  "registro_id" integer,
  "fecha_cambio" timestamp,
  "usuario" text,
  "registro_anterior" text,
  "registro_nuevo" text
);

CREATE TABLE "auditoria_req_vs_oc" (
  "id" integer PRIMARY KEY,
  "registro_id" integer,
  "usuario" text,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb,
  "fecha_cambio" timestamptz
);

CREATE TABLE "bajas_equipos" (
  "id" integer PRIMARY KEY,
  "folio" text,
  "pdf_codigo" text,
  "archivo_pdf" varchar,
  "estado" text,
  "planta" text,
  "fecha" date,
  "equipo" text,
  "marca" text,
  "modelo" text,
  "no_serie" text,
  "activo_fijo" text,
  "ubicacion_persona" text,
  "motivo_de_baja" text,
  "diagnostico" text,
  "comentarios" text,
  "motivo_de_cancelacion" text,
  "tiene_pdf" boolean,
  "fecha_creacion" timestamptz
);

CREATE TABLE "bajas_historial" (
  "id" integer PRIMARY KEY,
  "baja_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "bitacora_firecom" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "orden_servicio" text,
  "fecha" date,
  "persona_que_solicita_reporta" text,
  "cuenta_con_poliza" boolean,
  "servicio_con_costo" boolean,
  "ubicacion" text,
  "area" text,
  "cantidad" integer,
  "descripcion_servicio" text,
  "descripcion_trabajo" text,
  "material_equipo" text,
  "observaciones" text,
  "proveedores" text,
  "panel_faceplate" text,
  "switch_red" text,
  "personal_que_recibio" text,
  "pagado" boolean,
  "oc2" text,
  "created_at" timestamp
);

CREATE TABLE "bitacora_firecom_historial" (
  "id" integer PRIMARY KEY,
  "registro_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "calendario_config" (
  "clave" varchar PRIMARY KEY,
  "valor" text,
  "actualizado_en" timestamptz
);

CREATE TABLE "calendario_estado" (
  "id" integer PRIMARY KEY,
  "planta_key" varchar,
  "periodo" smallint,
  "semana_inicio" smallint,
  "anio_inicio" smallint,
  "generado" boolean,
  "terminado" boolean,
  "creado_en" timestamptz,
  "terminado_en" timestamptz
);

CREATE TABLE "camaras_audio" (
  "id" integer PRIMARY KEY,
  "oc" text,
  "folio_inventario" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "tipo" text,
  "marca" text,
  "modelo" text,
  "numero_de_serie" text,
  "proveedor" text,
  "cantidad" integer,
  "costo" numeric,
  "moneda" text,
  "destino" text,
  "accesorios" text,
  "fecha_de_salida" date,
  "planta" text,
  "destino2" text,
  "personal_it_que_asigna" text,
  "folio_de_servicio" text
);

CREATE TABLE "camaras_audio_historial" (
  "id" integer PRIMARY KEY,
  "camara_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "consumibles_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio_cantidad" text,
  "fecha_entrada" date,
  "recibido_por" text,
  "subcategoria" text,
  "marca" text,
  "modelo" text,
  "descripcion" text,
  "cantidad" integer,
  "proveedor" text,
  "costo" numeric,
  "moneda" text,
  "planta" text,
  "ubicacion" text,
  "destino" text
);

CREATE TABLE "consumibles_nf_historial" (
  "id" integer PRIMARY KEY,
  "consumible_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "dispositivos_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "marca" text,
  "modelo" text,
  "numero_serie" text,
  "cantidad" integer,
  "costo" numeric,
  "proveedor" text,
  "activo_fijo" text,
  "procesador" text,
  "arquitectura" text,
  "almacenamiento" text,
  "tipo_disco_duro" text,
  "sistema_operativo" text,
  "licencia_sistema_operativo" text,
  "memoria_ram" text,
  "velocidad_memoria" text,
  "tipo_memoria" text,
  "slot_memoria" text,
  "max_memoria" text,
  "modelo_cargador" text,
  "no_serie_eliminador" text,
  "bateria_laptop" text,
  "wifi_mac" text,
  "eth_mac" text,
  "accesorios" text,
  "ubicacion" text,
  "edificio" text,
  "fecha_salida" date,
  "asignado_a" text,
  "destino_planta" text,
  "disponible" boolean,
  "personal_it_que_asigna" text,
  "formato_de_baja" text,
  "created_at" timestamp
);

CREATE TABLE "dispositivos_nf_historial" (
  "id" integer PRIMARY KEY,
  "dispositivo_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "equipo_red_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio_correctivo" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "no_parte" text,
  "marca" text,
  "modelo" text,
  "numero_serie" text,
  "mac1" text,
  "mac2" text,
  "mac_address" text,
  "cantidad" integer,
  "proveedor" text,
  "costo" text,
  "moneda" text,
  "ubicacion" text,
  "observaciones_comentarios" text,
  "destino" text,
  "observaciones" text,
  "activo_dtr3" text
);

CREATE TABLE "equipo_red_nf_historial" (
  "id" integer PRIMARY KEY,
  "equipo_red_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "herramientas_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio_correctivo" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "tipo_uso" text,
  "marca" text,
  "modelo" text,
  "cantidad" integer,
  "numero_serie" text,
  "num_parte" text,
  "costo" numeric,
  "moneda" text,
  "proveedor" text,
  "ubicacion" text,
  "comentarios" text,
  "created_at" timestamp
);

CREATE TABLE "herramientas_nf_historial" (
  "id" integer PRIMARY KEY,
  "herramienta_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "impresoras_info" (
  "id" integer PRIMARY KEY,
  "impresora" text,
  "modelo" text,
  "numero_de_serie" text,
  "ip" text,
  "ubicacion" text,
  "planta" text,
  "identificador" text,
  "numero" integer
);

CREATE TABLE "impresoras_info_historial" (
  "id" integer PRIMARY KEY,
  "impresora_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "impresoras_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio_inventario" text,
  "fecha_de_entrada" date,
  "recibido_por" text,
  "marca" text,
  "modelo" text,
  "numero_de_serie" text,
  "tipo" text,
  "cantidad" integer,
  "ip" text,
  "mac" text,
  "proveedor" text,
  "costo" numeric,
  "moneda" text,
  "ubicacion" text,
  "estado" text,
  "planta" text,
  "disponible" text,
  "fecha_de_asignacion" date,
  "observaciones" text,
  "fecha_de_salida" date,
  "destino_planta" text,
  "asignado_a" text,
  "personal_it_que_asigna" text,
  "fecha_de_mantenimiento" date
);

CREATE TABLE "impresoras_nf_historial" (
  "id" integer PRIMARY KEY,
  "impresora_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "inventarios_nf" (
  "id" integer PRIMARY KEY,
  "inv_folio" text,
  "equipo" text,
  "marca" text,
  "modelo" text,
  "cantidad" integer,
  "precio_unitario" numeric,
  "precio_con_iva" numeric,
  "moneda" text,
  "proveedor" text,
  "presupuesto" text,
  "status" text,
  "anio" integer,
  "oc" text,
  "numero_serie" text,
  "ubicacion_actual" text,
  "created_at" timestamp
);

CREATE TABLE "inventarios_nf_historial" (
  "id" integer PRIMARY KEY,
  "inventario_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "mantenimientos_correctivos" (
  "id" integer PRIMARY KEY,
  "status" varchar,
  "folio" varchar,
  "planta" varchar,
  "linea_persona" varchar,
  "equipo" varchar,
  "marca" varchar,
  "modelo" varchar,
  "numero_serie" varchar,
  "descripcion_falla" text,
  "accesorio_solicitado" text,
  "fecha_solicitud" date,
  "reporte_elaborado_por" varchar,
  "tipo_observacion" varchar,
  "vencimiento_dias" integer,
  "fecha_conteo_actual" date,
  "fecha_limite_cierre" date,
  "categoria_correctivo" varchar,
  "refaccion_accesorio_compra" text,
  "fecha_llegada_refaccion" date,
  "fecha_reparacion" date,
  "quien_realizo_reparacion" varchar,
  "validacion_funcionamiento" varchar,
  "descripcion_reparacion" text,
  "observaciones" text,
  "oc_factura" varchar,
  "pdf" varchar
);

CREATE TABLE "mantenimientos_preventivos" (
  "id" bigint PRIMARY KEY,
  "id_equipo" varchar,
  "ubicacion" varchar,
  "plazo" varchar,
  "realizado_por" varchar,
  "fecha_realizacion" date,
  "observaciones" text,
  "planta" varchar,
  "categoria_color" varchar,
  "nombre_dispositivo" varchar,
  "pdf" varchar,
  "preventivo_digital" jsonb,
  "anio_creacion" integer,
  "preventivo_digital_p2" jsonb,
  "fecha_realizacion_p2" timestamp,
  "plazo_p2" date,
  "realizado_por_p2" text
);

CREATE TABLE "ordenes_de_compra" (
  "id" integer PRIMARY KEY,
  "orden_de_compra" text,
  "folio" text,
  "solicitante" text,
  "presupuesto_mes" text,
  "serie_ubicacion_no_empleado" text,
  "accesorio_solicitado" text,
  "proveedor_elegido" text,
  "pieza_servicio" text,
  "cantidad" numeric,
  "precio_unitario" numeric,
  "total_sin_iva" numeric,
  "moneda" text,
  "comentarios" text,
  "hoja_control" text,
  "requisicion" text,
  "fecha_oc" date,
  "oc" text,
  "fecha_entrada" date,
  "cantidad_registrada" numeric,
  "estatus_oc" text,
  "pdf" text
);

CREATE TABLE "pantallas_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "marca" text,
  "modelo" text,
  "no_serie" text,
  "cantidad" integer,
  "tamano_pulgadas" numeric,
  "accesorios" text,
  "mac_wifi" text,
  "mac_ethernet" text,
  "proveedor" text,
  "costo_usd" numeric,
  "vida_util_meses" integer,
  "estado" text,
  "disponible" boolean,
  "fecha_salida" date,
  "destino_planta" text,
  "asignado_a" text,
  "personal_it_que_asigna" text,
  "fecha_creacion" timestamptz
);

CREATE TABLE "pantallas_nf_historial" (
  "id" integer PRIMARY KEY,
  "pantalla_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "perifericos_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio_inventario" text,
  "fecha_entrada" date,
  "recibido_por" text,
  "subcategoria" text,
  "tipo" text,
  "marca" text,
  "modelo" text,
  "cantidad" integer,
  "numero_serie" text,
  "proveedor" text,
  "costo_pesos" numeric,
  "estado" text,
  "destino" text,
  "disponible" boolean,
  "fecha_salida" date,
  "destino_planta" text,
  "asignado_a" text,
  "personal_it_que_asigna" text,
  "created_at" timestamp
);

CREATE TABLE "perifericos_nf_historial" (
  "id" integer PRIMARY KEY,
  "periferico_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "radios_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "marca" text,
  "modelo" text,
  "no_serie" text,
  "cantidad" integer,
  "observaciones" text,
  "proveedor" text,
  "costo" numeric,
  "moneda" text,
  "vida_util" text,
  "requisitor" text,
  "disponible" boolean,
  "fecha_salida" date,
  "asignado_a" text,
  "destino_planta" text,
  "personal_it_asigna" text
);

CREATE TABLE "radios_nf_historial" (
  "id" integer PRIMARY KEY,
  "radio_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "refacciones_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "folio_correctivo" text,
  "fecha_registro" date,
  "recibido_por" text,
  "subcategoria" text,
  "marca" text,
  "modelo" text,
  "serie" text,
  "cantidad" integer,
  "num_parte" text,
  "costo" numeric,
  "moneda" text,
  "proveedor" text,
  "disponible" text,
  "comentarios" text
);

CREATE TABLE "refacciones_nf_historial" (
  "id" integer PRIMARY KEY,
  "refaccion_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "registro_entradas_temporal" (
  "id" integer PRIMARY KEY,
  "id_registro" text,
  "id_oc" text,
  "serie" text,
  "folio" text,
  "oc" text,
  "fecha_ingreso" date,
  "proveedor" text,
  "recibido_por" text,
  "categoria" text,
  "subcategoria" text,
  "tipo" text,
  "marca" text,
  "modelo" text,
  "ubicacion" text,
  "comentarios" text,
  "precio_unitario" numeric,
  "moneda" text,
  "status_entrada" text,
  "fecha_salida" date,
  "destino" text,
  "responsable" text,
  "pdf" text
);

CREATE TABLE "remisiones" (
  "id" integer PRIMARY KEY,
  "id_oc" text,
  "id_remision" text,
  "folio" text,
  "solicitante" text,
  "fecha_solicitud" date,
  "accesorio_solicitado" text,
  "modelo_serie_comentarios" text,
  "proveedor" text,
  "pieza_servicio" text,
  "cantidad" integer,
  "precio_unitario" numeric,
  "total_sin_iva" numeric,
  "moneda" text,
  "pagado" boolean,
  "presupuesto" text,
  "cuenta_a_descontar" text,
  "fecha_entrada_planta" date,
  "status" text,
  "requisicion" text,
  "oc" text
);

CREATE TABLE "remisiones_historial" (
  "id" integer PRIMARY KEY,
  "remision_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "reportes_impresoras" (
  "id" integer PRIMARY KEY,
  "folio" text,
  "fecha" date,
  "planta" text,
  "impresora" text,
  "area" text,
  "reporte" text,
  "quien_reporta" text,
  "estatus" text,
  "fecha_de_realizacion" date,
  "comentarios" text
);

CREATE TABLE "reportes_impresoras_historial" (
  "id" integer PRIMARY KEY,
  "reporte_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "req_vs_oc" (
  "id" integer PRIMARY KEY,
  "no_requisicion" text,
  "orden_compra" text,
  "fecha_compra" date,
  "po_subtotal" numeric,
  "moneda" text,
  "oc_subtotal" text,
  "registrada_en_oc" text,
  "pdf" text
);

CREATE TABLE "servicios_proveedores" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "folio_cotizacion" text,
  "folio_reporte" text,
  "fecha" date,
  "requisitor" text,
  "cuenta_con_poliza" boolean,
  "servicio_con_costo" boolean,
  "ubicacion_planta" text,
  "area" text,
  "cantidad" integer,
  "descripcion_servicio" text,
  "descripcion_trabajo" text,
  "material_equipo" text,
  "observaciones" text,
  "proveedores" text,
  "panel_faceplate" text,
  "switch" text,
  "personal_recibio" text,
  "solicitud_finalizada" boolean,
  "costo" numeric,
  "folio_unico" text
);

CREATE TABLE "servicios_proveedores_historial" (
  "id" integer PRIMARY KEY,
  "servicio_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "tintas_toner_ribon_nf" (
  "id" integer PRIMARY KEY,
  "id_unico" text,
  "oc" text,
  "modelo" text,
  "recibido_por" text,
  "subcategoria" text,
  "fecha_registro" date,
  "proveedor" text,
  "stock" integer,
  "costo_mn" numeric,
  "cantidad_recibida" integer,
  "fecha_instalacion" date,
  "ubicacion" text,
  "impresora" text,
  "instalado_por" text
);

CREATE TABLE "tintas_toner_ribon_nf_historial" (
  "id" integer PRIMARY KEY,
  "tinta_id" integer,
  "usuario" text,
  "fecha" timestamptz,
  "registro_anterior" jsonb,
  "registro_nuevo" jsonb
);

CREATE TABLE "usuarios" (
  "id" integer PRIMARY KEY,
  "usuario" varchar,
  "nombre" varchar,
  "password_hash" varchar,
  "rol" varchar,
  "password_temporal" boolean,
  "activo" boolean,
  "creado_en" timestamp,
  "ultimo_acceso" timestamp
);

ALTER TABLE "accesorios_nf_historial" ADD FOREIGN KEY ("accesorio_id") REFERENCES "accesorios_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "bajas_historial" ADD FOREIGN KEY ("baja_id") REFERENCES "bajas_equipos" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "camaras_audio_historial" ADD FOREIGN KEY ("camara_id") REFERENCES "camaras_audio" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "consumibles_nf_historial" ADD FOREIGN KEY ("consumible_id") REFERENCES "consumibles_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "dispositivos_nf_historial" ADD FOREIGN KEY ("dispositivo_id") REFERENCES "dispositivos_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "equipo_red_nf_historial" ADD FOREIGN KEY ("equipo_red_id") REFERENCES "equipo_red_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "herramientas_nf_historial" ADD FOREIGN KEY ("herramienta_id") REFERENCES "herramientas_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "impresoras_info_historial" ADD FOREIGN KEY ("impresora_id") REFERENCES "impresoras_info" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "impresoras_nf_historial" ADD FOREIGN KEY ("impresora_id") REFERENCES "impresoras_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "inventarios_nf_historial" ADD FOREIGN KEY ("inventario_id") REFERENCES "inventarios_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "pantallas_nf_historial" ADD FOREIGN KEY ("pantalla_id") REFERENCES "pantallas_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "perifericos_nf_historial" ADD FOREIGN KEY ("periferico_id") REFERENCES "perifericos_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "radios_nf_historial" ADD FOREIGN KEY ("radio_id") REFERENCES "radios_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "refacciones_nf_historial" ADD FOREIGN KEY ("refaccion_id") REFERENCES "refacciones_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "remisiones_historial" ADD FOREIGN KEY ("remision_id") REFERENCES "remisiones" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "reportes_impresoras_historial" ADD FOREIGN KEY ("reporte_id") REFERENCES "reportes_impresoras" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "servicios_proveedores_historial" ADD FOREIGN KEY ("servicio_id") REFERENCES "servicios_proveedores" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "tintas_toner_ribon_nf_historial" ADD FOREIGN KEY ("tinta_id") REFERENCES "tintas_toner_ribon_nf" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "auditoria_correctivos" ADD FOREIGN KEY ("registro_id") REFERENCES "mantenimientos_correctivos" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "auditoria_ordenes_de_compra" ADD FOREIGN KEY ("registro_id") REFERENCES "ordenes_de_compra" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "auditoria_preventivos" ADD FOREIGN KEY ("registro_id") REFERENCES "mantenimientos_preventivos" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "auditoria_registro_entradas_temporal" ADD FOREIGN KEY ("registro_id") REFERENCES "registro_entradas_temporal" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "auditoria_req_vs_oc" ADD FOREIGN KEY ("registro_id") REFERENCES "req_vs_oc" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "remisiones" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "req_vs_oc" ADD FOREIGN KEY ("orden_compra") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "registro_entradas_temporal" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "dispositivos_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "impresoras_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "perifericos_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "radios_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "pantallas_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "accesorios_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "herramientas_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "refacciones_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "equipo_red_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "camaras_audio" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "consumibles_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "tintas_toner_ribon_nf" ADD FOREIGN KEY ("oc") REFERENCES "ordenes_de_compra" ("oc") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "mantenimientos_preventivos" ADD FOREIGN KEY ("id_equipo") REFERENCES "dispositivos_nf" ("activo_fijo") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "mantenimientos_correctivos" ADD FOREIGN KEY ("numero_serie") REFERENCES "dispositivos_nf" ("numero_serie") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "accesorios_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "bajas_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "bitacora_firecom_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "camaras_audio_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "consumibles_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "dispositivos_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "equipo_red_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "herramientas_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "impresoras_info_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "impresoras_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "inventarios_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "pantallas_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "perifericos_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "radios_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "refacciones_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "remisiones_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "reportes_impresoras_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "servicios_proveedores_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "tintas_toner_ribon_nf_historial" ADD FOREIGN KEY ("usuario") REFERENCES "usuarios" ("usuario") DEFERRABLE INITIALLY IMMEDIATE;
