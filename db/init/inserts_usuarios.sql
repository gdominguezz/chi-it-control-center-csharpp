--
-- PostgreSQL database dump
--

\restrict hsoGrUPBHuyQbAsNK97dIsvfYLZeifIRicygJLYxsVaPo1OMztupPRA2VCCtxFJ

-- Dumped from database version 18.2
-- Dumped by pg_dump version 18.2

-- Started on 2026-03-13 16:45:10

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 5191 (class 0 OID 17253)
-- Dependencies: 276
-- Data for Name: usuarios; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (1, 'RODRIGUEZFL', 'FLOR RODRIGUEZ', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'ADMIN', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (2, 'CHAVEZS', 'CHAVEZ SANTIAGO', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (3, 'SIFUENTESR', 'SIFUENTES RAUL', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (4, 'GARCIALU', 'GARCIA LUIS', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (5, 'GANDARAJA', 'GANDARA ANGEL', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (6, 'VLOPEZ', 'VIANEY LOPEZ', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (7, 'LUNAI', 'LUNA ISMAEL', '1538369f0f96d56db220a34e9707507fd19df647c1050e30add2063d635c7d09', 'USUARIO', false, true, '2026-03-11 15:44:04.187177', '2026-03-03 17:29:52.877548');
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (8, 'PENAV', 'PEÑA VICTOR', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', '2026-03-09 23:49:17.724481');
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (9, 'X', 'X', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525', 'X', NULL, NULL, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (10, 'REYESR', 'RODRIGO REYES', 'd4c2e9d7f2e2b6f7a52c57d20593d3536bc3a1e9bdfb1ef82b1f345962f8d833', 'USUARIO', true, true, '2026-03-11 15:44:04.187177', NULL);
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (11, 'DOMINGUEZT', 'TRISTAN DOMINGUEZ', '104fe2f79c46473abe8238d0c53bda0ad793aa23615bbf2e8d7083f27703dd11', 'ADMIN', false, true, '2026-03-11 15:44:04.187177', '2026-03-13 15:45:26.968039');
INSERT INTO public.usuarios OVERRIDING SYSTEM VALUE VALUES (12, 'ADMIN', 'Tristan', 'b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d', 'ADMIN', false, true, '2026-03-13 16:15:42.779243', NULL);


--
-- TOC entry 5197 (class 0 OID 0)
-- Dependencies: 275
-- Name: usuarios_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.usuarios_id_seq', 12, true);


-- Completed on 2026-03-13 16:45:10

--
-- PostgreSQL database dump complete
--

\unrestrict hsoGrUPBHuyQbAsNK97dIsvfYLZeifIRicygJLYxsVaPo1OMztupPRA2VCCtxFJ

