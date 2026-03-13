-- Table: public.usuarios

-- DROP TABLE IF EXISTS public.usuarios;

CREATE TABLE IF NOT EXISTS public.usuarios
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    usuario character varying(100) COLLATE pg_catalog."default" NOT NULL,
    nombre character varying(100) COLLATE pg_catalog."default" NOT NULL,
    password_hash character varying(255) COLLATE pg_catalog."default" NOT NULL,
    rol character varying(50) COLLATE pg_catalog."default" NOT NULL,
    password_temporal boolean DEFAULT true,
    activo boolean DEFAULT true,
    creado_en timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    ultimo_acceso timestamp without time zone,
    CONSTRAINT usuarios_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.usuarios
    OWNER to postgres;



-- Table: public.mantenimientos_preventivos

-- DROP TABLE IF EXISTS public.mantenimientos_preventivos;

CREATE TABLE IF NOT EXISTS public.mantenimientos_preventivos
(
    id bigint NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
    id_equipo character varying(50) COLLATE pg_catalog."default",
    ubicacion character varying(100) COLLATE pg_catalog."default",
    plazo character varying(50) COLLATE pg_catalog."default",
    realizado_por character varying(100) COLLATE pg_catalog."default",
    fecha_realizacion date,
    observaciones text COLLATE pg_catalog."default",
    planta character varying(20) COLLATE pg_catalog."default",
    categoria_color character varying(50) COLLATE pg_catalog."default",
    nombre_dispositivo character varying(100) COLLATE pg_catalog."default",
    pdf character varying COLLATE pg_catalog."default",
    preventivo_digital jsonb,
    CONSTRAINT mantenimientos_preventivos_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.mantenimientos_preventivos
    OWNER to postgres;


-- Table: public.auditoria_preventivos

-- DROP TABLE IF EXISTS public.auditoria_preventivos;

CREATE TABLE IF NOT EXISTS public.auditoria_preventivos
(
    id integer NOT NULL DEFAULT nextval('auditoria_preventivos_id_seq'::regclass),
    registro_id integer NOT NULL,
    fecha_cambio timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    usuario character varying(100) COLLATE pg_catalog."default",
    registro_anterior text COLLATE pg_catalog."default",
    registro_nuevo text COLLATE pg_catalog."default",
    CONSTRAINT auditoria_preventivos_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.auditoria_preventivos
    OWNER to postgres;