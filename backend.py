from fastapi import FastAPI, UploadFile, File, Query
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse, Response
from pydantic import BaseModel
import psycopg2
from typing import Optional
import io
import pandas as pd
from fastapi import Query, UploadFile, File
from fastapi.responses import StreamingResponse, Response
import os
from pathlib import Path

app = FastAPI()

# ==========================================================
# CONEXION
# ==========================================================

def get_connection():
    return psycopg2.connect(
        host="127.0.0.1",
        database="SISTEMAS",
        user="postgres",
        password="Tristan2468"
    )

# ==========================================================
# CONFIGURACION PDF
# ==========================================================
# Para cambiar al servidor: solo modifica PDF_BASE_PATH
# PDF_BASE_PATH = r"\\chispeedy\Repository\PDF_DATABASE"
PDF_BASE_PATH = r"C:\Users\dominguezg\Desktop\archivos\PDF_DATABASE"

def get_pdf_path(modulo: str, id: int) -> Path:
    carpeta = Path(PDF_BASE_PATH) / modulo
    carpeta.mkdir(parents=True, exist_ok=True)
    return carpeta / f"{id}.pdf"

# ==========================================================
# ARCHIVOS ESTATICOS
# ==========================================================

app.mount("/static", StaticFiles(directory="static"), name="static")

@app.get("/")
def root():
    return FileResponse("static/menu.html")

# ==========================================================
# ================= PREVENTIVOS =============================
# ==========================================================

class Preventivo(BaseModel):
    ID_EQUIPO: Optional[str] = None
    UBICACION: Optional[str] = None
    PLAZO: Optional[str] = None
    REALIZADO_POR: Optional[str] = None
    FECHA_REALIZACION: Optional[str] = None
    OBSERVACIONES: Optional[str] = None
    nombre_dispositivo: Optional[str] = None
    PLANTA: Optional[str] = None
    CATEGORIA_COLOR: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/PREVENTIVO")
def crear_preventivo(data: Preventivo):
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
        INSERT INTO public."MANTENIMIENTOS_PREVENTIVOS"
        ("ID_EQUIPO","UBICACION","PLAZO","REALIZADO_POR",
         "FECHA_REALIZACION","OBSERVACIONES",
         "nombre_dispositivo","PLANTA","CATEGORIA_COLOR")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "Id";
    """, (
        data.ID_EQUIPO,
        data.UBICACION,
        data.PLAZO,
        data.REALIZADO_POR,
        data.FECHA_REALIZACION,
        data.OBSERVACIONES,
        data.nombre_dispositivo,
        data.PLANTA,
        data.CATEGORIA_COLOR
    ))

    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()

    return {"ID": new_id}

# ---------------- EDITAR ----------------
@app.put("/PREVENTIVO/{id}")
def editar_preventivo(id: int, data: Preventivo):
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
        UPDATE public."MANTENIMIENTOS_PREVENTIVOS"
        SET
        "ID_EQUIPO"=%s,
        "UBICACION"=%s,
        "PLAZO"=%s,
        "REALIZADO_POR"=%s,
        "FECHA_REALIZACION"=%s,
        "OBSERVACIONES"=%s,
        "nombre_dispositivo"=%s,
        "PLANTA"=%s,
        "CATEGORIA_COLOR"=%s
        WHERE "Id"=%s
    """, (
        data.ID_EQUIPO,
        data.UBICACION,
        data.PLAZO,
        data.REALIZADO_POR,
        data.FECHA_REALIZACION,
        data.OBSERVACIONES,
        data.nombre_dispositivo,
        data.PLANTA,
        data.CATEGORIA_COLOR,
        id
    ))

    conn.commit()
    cursor.close()
    conn.close()

    return {"mensaje": "ACTUALIZADO"}

# ---------------- PAGINACION (SIN PDF BYTEA) ----------------
@app.get("/PREVENTIVOS")
def obtener_preventivos(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_EQUIPO: str = None,
    UBICACION: str = None,
    PLAZO: str = None,
    REALIZADO_POR: str = None,
    FECHA_REALIZACION: str = None,
    OBSERVACIONES: str = None,
    nombre_dispositivo: str = None,
    PLANTA: str = None,
    CATEGORIA_COLOR: str = None
):

    offset = (page - 1) * limit 

    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def agregar_filtro(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    agregar_filtro("ID_EQUIPO", ID_EQUIPO)
    agregar_filtro("UBICACION", UBICACION)
    agregar_filtro("PLAZO", PLAZO)
    agregar_filtro("REALIZADO_POR", REALIZADO_POR)
    agregar_filtro("FECHA_REALIZACION", FECHA_REALIZACION)
    agregar_filtro("OBSERVACIONES", OBSERVACIONES)
    agregar_filtro("nombre_dispositivo", nombre_dispositivo)
    agregar_filtro("PLANTA", PLANTA)
    agregar_filtro("CATEGORIA_COLOR", CATEGORIA_COLOR)

    where_clause = ""
    if filtros:
        where_clause = "WHERE " + " AND ".join(filtros)

    # TOTAL CON FILTROS
    cursor.execute(f'''
        SELECT COUNT(*)
        FROM public."MANTENIMIENTOS_PREVENTIVOS"
        {where_clause}
    ''', params)

    total = cursor.fetchone()[0]

    # CONSULTA PAGINADA
    cursor.execute(f'''
        SELECT
        "Id",
        "ID_EQUIPO",
        "UBICACION",
        "PLAZO",
        "REALIZADO_POR",
        "FECHA_REALIZACION",
        "OBSERVACIONES",
        "nombre_dispositivo",
        "PLANTA",
        "CATEGORIA_COLOR",
        CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."MANTENIMIENTOS_PREVENTIVOS"
        {where_clause}
        ORDER BY "Id" DESC
        LIMIT %s OFFSET %s
    ''', params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()

    return {
        "total": total,
        "page": page,
        "limit": limit,
        "data": resultado
    }

@app.get("/PREVENTIVOS/EXPORTAR")
def exportar_preventivos(
    ID_EQUIPO: str = None,
    UBICACION: str = None,
    PLAZO: str = None,
    REALIZADO_POR: str = None,
    FECHA_REALIZACION: str = None,
    OBSERVACIONES: str = None,
    nombre_dispositivo: str = None,
    PLANTA: str = None,
    CATEGORIA_COLOR: str = None
):

    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def agregar(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    agregar("ID_EQUIPO", ID_EQUIPO)
    agregar("UBICACION", UBICACION)
    agregar("PLAZO", PLAZO)
    agregar("REALIZADO_POR", REALIZADO_POR)
    agregar("FECHA_REALIZACION", FECHA_REALIZACION)
    agregar("OBSERVACIONES", OBSERVACIONES)
    agregar("nombre_dispositivo", nombre_dispositivo)
    agregar("PLANTA", PLANTA)
    agregar("CATEGORIA_COLOR", CATEGORIA_COLOR)

    where = ""
    if filtros:
        where = "WHERE " + " AND ".join(filtros)

    cursor.execute(f'''
        SELECT *
        FROM public."MANTENIMIENTOS_PREVENTIVOS"
        {where}
        ORDER BY "Id" DESC
    ''', params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]

    df = pd.DataFrame(rows, columns=columns)

    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)

    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=preventivos_filtrados.xlsx"}
    )

@app.get("/PREVENTIVOS/EXPORTAR_TODO")
def exportar_todo_preventivos():

    conn = get_connection()

    df = pd.read_sql(
        'SELECT * FROM public."MANTENIMIENTOS_PREVENTIVOS" ORDER BY "Id" DESC',
        conn
    )

    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)

    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=preventivos_todo.xlsx"}
    )

@app.get("/PREVENTIVOS/EXPORTAR_ANIO")
def exportar_preventivos_por_anio(anio: int):

    conn = get_connection()

    df = pd.read_sql("""
        SELECT *
        FROM public."MANTENIMIENTOS_PREVENTIVOS"
        WHERE EXTRACT(YEAR FROM "FECHA_REALIZACION") = %s
        ORDER BY "Id" DESC
    """, conn, params=(anio,))

    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)

    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={
            "Content-Disposition": f"attachment; filename=preventivos_{anio}.xlsx"
        }
    )
# ---------------- ELIMINAR PDF ----------------
@app.delete("/PREVENTIVO/PDF/{id}")
def eliminar_PDF_preventivo(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."MANTENIMIENTOS_PREVENTIVOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."MANTENIMIENTOS_PREVENTIVOS" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}
# ---------------- ELIMINAR ----------------
@app.delete("/PREVENTIVO/{id}")
def eliminar_preventivo(id: int):
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute(
        'DELETE FROM public."MANTENIMIENTOS_PREVENTIVOS" WHERE "Id"=%s',
        (id,)
    )

    conn.commit()
    cursor.close()
    conn.close()

    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/PREVENTIVO/PDF/{id}")
async def subir_PDF_preventivo(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("PREVENTIVO", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."MANTENIMIENTOS_PREVENTIVOS" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/PREVENTIVO/PDF/{id}")
def ver_PDF_preventivo(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."MANTENIMIENTOS_PREVENTIVOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

@app.get("/CORRECTIVOS/EXPORTAR_TODO")
def exportar_correctivos_todo():

    conn = get_connection()

    df = pd.read_sql(
        'SELECT * FROM public."MANTENIMIENTOS_CORRECTIVOS" ORDER BY "ID" DESC',
        conn
    )

    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)

    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=correctivos_todo.xlsx"}
    )
@app.get("/CORRECTIVOS/EXPORTAR_ANIO")
def exportar_correctivos_por_anio(anio: int):

    conn = get_connection()

    df = pd.read_sql("""
        SELECT *
        FROM public."MANTENIMIENTOS_CORRECTIVOS"
        WHERE EXTRACT(YEAR FROM "FECHA_SOLICITUD") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))

    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)

    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={
            "Content-Disposition": f"attachment; filename=correctivos_{anio}.xlsx"
        }
    )


@app.post("/CORRECTIVO/PDF/{id}")
async def subir_PDF_correctivo(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("CORRECTIVO", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."MANTENIMIENTOS_CORRECTIVOS" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

@app.get("/CORRECTIVO/PDF/{id}")
def ver_PDF_correctivo(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."MANTENIMIENTOS_CORRECTIVOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")


@app.delete("/CORRECTIVO/PDF/{id}")
def eliminar_PDF_correctivo(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."MANTENIMIENTOS_CORRECTIVOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."MANTENIMIENTOS_CORRECTIVOS" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}
# ---------------- EXPORTAR TODO ----------------
@app.post("/BAJA/PDF/{id}")
async def subir_PDF_baja(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("BAJA", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."BAJAS_EQUIPOS" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

@app.get("/BAJA/PDF/{id}")
def ver_PDF_baja(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."BAJAS_EQUIPOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

@app.delete("/BAJA/PDF/{id}")
def eliminar_PDF_baja(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."BAJAS_EQUIPOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."BAJAS_EQUIPOS" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

@app.get("/BAJAS/EXPORTAR_TODO")
def exportar_todo_bajas():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","FOLIO","ESTADO","PLANTA","FECHA","EQUIPO","MARCA","MODELO",
               "NO_SERIE","ACTIVO_FIJO","UBICACION_PERSONA","MOTIVO_BAJA",
               "DIAGNOSTICO","COMENTARIOS","MOTIVO_CANCELACION"
        FROM public."BAJAS_EQUIPOS" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=bajas_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/BAJAS/EXPORTAR_ANIO")
def exportar_bajas_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","FOLIO","ESTADO","PLANTA","FECHA","EQUIPO","MARCA","MODELO",
               "NO_SERIE","ACTIVO_FIJO","UBICACION_PERSONA","MOTIVO_BAJA",
               "DIAGNOSTICO","COMENTARIOS","MOTIVO_CANCELACION"
        FROM public."BAJAS_EQUIPOS"
        WHERE EXTRACT(YEAR FROM "FECHA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=bajas_{anio}.xlsx"}
    )




# ==========================================================
# =================== PRESUPUESTOS =========================
# ==========================================================

# ==========================================================
# =================== PRESUPUESTOS_REQ_VS_OC =========================
# ==========================================================

# ==========================================================
# ================ REQ vs ORDEN DE COMPRA ==================
# ==========================================================

class ReqOC(BaseModel):
    NO_REQUISICION:   Optional[str] = None
    ORDEN_COMPRA:     Optional[str] = None
    FECHA_COMPRA:     Optional[str] = None
    PO_SUBTOTAL:      Optional[str] = None
    MONEDA:           Optional[str] = None
    OC_SUBTOTAL:      Optional[str] = None
    REGISTRADA_EN_OC: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/PRESUPUESTOS_REQ_VS_OC")
def crear_PRESUPUESTOS_REQ_VS_OC(data: ReqOC):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."PRESUPUESTOS_REQ_VS_OC"
        ("NO_REQUISICION","ORDEN_COMPRA","FECHA_COMPRA",
         "PO_SUBTOTAL","MONEDA","OC_SUBTOTAL","REGISTRADA_EN_OC")
        VALUES (%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.NO_REQUISICION, data.ORDEN_COMPRA, data.FECHA_COMPRA,
        data.PO_SUBTOTAL, data.MONEDA, data.OC_SUBTOTAL, data.REGISTRADA_EN_OC
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- EDITAR ----------------
@app.put("/PRESUPUESTOS_REQ_VS_OC/{id}")
def editar_PRESUPUESTOS_REQ_VS_OC(id: int, data: ReqOC):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."PRESUPUESTOS_REQ_VS_OC"
        SET "NO_REQUISICION"=%s, "ORDEN_COMPRA"=%s, "FECHA_COMPRA"=%s,
            "PO_SUBTOTAL"=%s, "MONEDA"=%s, "OC_SUBTOTAL"=%s,
            "REGISTRADA_EN_OC"=%s
        WHERE "ID"=%s
    """, (
        data.NO_REQUISICION, data.ORDEN_COMPRA, data.FECHA_COMPRA,
        data.PO_SUBTOTAL, data.MONEDA, data.OC_SUBTOTAL,
        data.REGISTRADA_EN_OC, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/PRESUPUESTOS_REQ_VS_OC/{id}")
def eliminar_PRESUPUESTOS_REQ_VS_OC(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."PRESUPUESTOS_REQ_VS_OC" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- PAGINACION ----------------
@app.get("/PRESUPUESTOS_REQ_VS_OC")
def obtener_PRESUPUESTOS_REQ_VS_OC(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    NO_REQUISICION:   str = None,
    ORDEN_COMPRA:     str = None,
    FECHA_COMPRA:     str = None,
    PO_SUBTOTAL:      str = None,
    MONEDA:           str = None,
    OC_SUBTOTAL:      str = None,
    REGISTRADA_EN_OC: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def agregar_filtro(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    agregar_filtro("NO_REQUISICION",   NO_REQUISICION)
    agregar_filtro("ORDEN_COMPRA",     ORDEN_COMPRA)
    agregar_filtro("FECHA_COMPRA",     FECHA_COMPRA)
    agregar_filtro("PO_SUBTOTAL",      PO_SUBTOTAL)
    agregar_filtro("MONEDA",           MONEDA)
    agregar_filtro("OC_SUBTOTAL",      OC_SUBTOTAL)
    agregar_filtro("REGISTRADA_EN_OC", REGISTRADA_EN_OC)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."PRESUPUESTOS_REQ_VS_OC" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f'''
        SELECT
            "ID", "NO_REQUISICION", "ORDEN_COMPRA", "FECHA_COMPRA",
            "PO_SUBTOTAL", "MONEDA", "OC_SUBTOTAL", "REGISTRADA_EN_OC",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."PRESUPUESTOS_REQ_VS_OC"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    ''', params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- SUBIR PDF ----------------
@app.post("/PRESUPUESTOS_REQ_VS_OC/PDF/{id}")
async def subir_PDF_PRESUPUESTOS_REQ_VS_OC(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("PRESUPUESTOS_REQ_VS_OC", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."PRESUPUESTOS_REQ_VS_OC" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/PRESUPUESTOS_REQ_VS_OC/PDF/{id}")
def ver_PDF_PRESUPUESTOS_REQ_VS_OC(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."PRESUPUESTOS_REQ_VS_OC" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")


@app.delete("/PRESUPUESTOS_REQ_VS_OC/PDF/{id}")
def eliminar_PDF_PRESUPUESTOS_REQ_VS_OC(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."PRESUPUESTOS_REQ_VS_OC" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."PRESUPUESTOS_REQ_VS_OC" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}
# ---------------- EXPORTAR TODO ----------------
@app.get("/PRESUPUESTOS_REQ_VS_OC/EXPORTAR_TODO")
def exportar_todo_PRESUPUESTOS_REQ_VS_OC():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","NO_REQUISICION","ORDEN_COMPRA","FECHA_COMPRA",
               "PO_SUBTOTAL","MONEDA","OC_SUBTOTAL","REGISTRADA_EN_OC"
        FROM public."PRESUPUESTOS_REQ_VS_OC" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=PRESUPUESTOS_REQ_VS_OC_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/PRESUPUESTOS_REQ_VS_OC/EXPORTAR_ANIO")
def exportar_PRESUPUESTOS_REQ_VS_OC_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","NO_REQUISICION","ORDEN_COMPRA","FECHA_COMPRA",
               "PO_SUBTOTAL","MONEDA","OC_SUBTOTAL","REGISTRADA_EN_OC"
        FROM public."PRESUPUESTOS_REQ_VS_OC"
        WHERE EXTRACT(YEAR FROM "FECHA_COMPRA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=PRESUPUESTOS_REQ_VS_OC_{anio}.xlsx"}
    )




# ==========================================================
# ================= ORDENES DE COMPRA ======================
# ==========================================================

class OrdenDeCompra(BaseModel):
    ORDEN_DE_COMPRA:                       Optional[str] = None
    FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO: Optional[str] = None
    SOLICITANTE:                           Optional[str] = None
    PRESUPUESTO_MES:                       Optional[str] = None
    SERIE_UBICACION_NO_EMPLEADO:           Optional[str] = None
    ACCESORIO_SOLICITADO:                  Optional[str] = None
    PROVEEDOR_ELEGIDO:                     Optional[str] = None
    PIEZA_SERVICIO:                        Optional[str] = None
    CANTIDAD:                              Optional[str] = None
    PRECIO_UNITARIO:                       Optional[str] = None
    TOTAL_NO_INCLUYE_IVA:                  Optional[str] = None
    MONEDA:                                Optional[str] = None
    COMENTARIOS:                           Optional[str] = None
    REQUISICION:                           Optional[str] = None
    FECHA_OC:                              Optional[str] = None
    OC:                                    Optional[str] = None
    FECHA_ENTRADA:                         Optional[str] = None
    CANTIDAD_REGISTRADA:                   Optional[str] = None
    ESTATUS_OC:                            Optional[str] = None
    HOJA_CONTROL:                          Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/ORDENES_DE_COMPRA")
def crear_orden_de_compra(data: OrdenDeCompra):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."ORDENES_DE_COMPRA"
        ("ORDEN_DE_COMPRA","FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO","SOLICITANTE",
         "PRESUPUESTO_MES","SERIE_UBICACION_NO_EMPLEADO","ACCESORIO_SOLICITADO",
         "PROVEEDOR_ELEGIDO","PIEZA_SERVICIO","CANTIDAD","PRECIO_UNITARIO",
         "TOTAL_NO_INCLUYE_IVA","MONEDA","COMENTARIOS","REQUISICION",
         "FECHA_OC","OC","FECHA_ENTRADA","CANTIDAD_REGISTRADA",
         "ESTATUS_OC","HOJA_CONTROL")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ORDEN_DE_COMPRA, data.FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO,
        data.SOLICITANTE, data.PRESUPUESTO_MES, data.SERIE_UBICACION_NO_EMPLEADO,
        data.ACCESORIO_SOLICITADO, data.PROVEEDOR_ELEGIDO, data.PIEZA_SERVICIO,
        data.CANTIDAD, data.PRECIO_UNITARIO, data.TOTAL_NO_INCLUYE_IVA,
        data.MONEDA, data.COMENTARIOS, data.REQUISICION, data.FECHA_OC,
        data.OC, data.FECHA_ENTRADA, data.CANTIDAD_REGISTRADA,
        data.ESTATUS_OC, data.HOJA_CONTROL
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- EDITAR ----------------
@app.put("/ORDENES_DE_COMPRA/{id}")
def editar_orden_de_compra(id: int, data: OrdenDeCompra):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."ORDENES_DE_COMPRA"
        SET
        "ORDEN_DE_COMPRA"=%s,
        "FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO"=%s,
        "SOLICITANTE"=%s,
        "PRESUPUESTO_MES"=%s,
        "SERIE_UBICACION_NO_EMPLEADO"=%s,
        "ACCESORIO_SOLICITADO"=%s,
        "PROVEEDOR_ELEGIDO"=%s,
        "PIEZA_SERVICIO"=%s,
        "CANTIDAD"=%s,
        "PRECIO_UNITARIO"=%s,
        "TOTAL_NO_INCLUYE_IVA"=%s,
        "MONEDA"=%s,
        "COMENTARIOS"=%s,
        "REQUISICION"=%s,
        "FECHA_OC"=%s,
        "OC"=%s,
        "FECHA_ENTRADA"=%s,
        "CANTIDAD_REGISTRADA"=%s,
        "ESTATUS_OC"=%s,
        "HOJA_CONTROL"=%s
        WHERE "ID"=%s
    """, (
        data.ORDEN_DE_COMPRA, data.FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO,
        data.SOLICITANTE, data.PRESUPUESTO_MES, data.SERIE_UBICACION_NO_EMPLEADO,
        data.ACCESORIO_SOLICITADO, data.PROVEEDOR_ELEGIDO, data.PIEZA_SERVICIO,
        data.CANTIDAD, data.PRECIO_UNITARIO, data.TOTAL_NO_INCLUYE_IVA,
        data.MONEDA, data.COMENTARIOS, data.REQUISICION, data.FECHA_OC,
        data.OC, data.FECHA_ENTRADA, data.CANTIDAD_REGISTRADA,
        data.ESTATUS_OC, data.HOJA_CONTROL, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/ORDENES_DE_COMPRA/{id}")
def eliminar_orden_de_compra(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."ORDENES_DE_COMPRA" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/ORDENES_DE_COMPRA")
def obtener_ordenes_de_compra(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ORDEN_DE_COMPRA:                       str = None,
    FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO: str = None,
    SOLICITANTE:                           str = None,
    PRESUPUESTO_MES:                       str = None,
    SERIE_UBICACION_NO_EMPLEADO:           str = None,
    ACCESORIO_SOLICITADO:                  str = None,
    PROVEEDOR_ELEGIDO:                     str = None,
    PIEZA_SERVICIO:                        str = None,
    CANTIDAD:                              str = None,
    PRECIO_UNITARIO:                       str = None,
    TOTAL_NO_INCLUYE_IVA:                  str = None,
    MONEDA:                                str = None,
    COMENTARIOS:                           str = None,
    REQUISICION:                           str = None,
    FECHA_OC:                              str = None,
    OC:                                    str = None,
    FECHA_ENTRADA:                         str = None,
    CANTIDAD_REGISTRADA:                   str = None,
    ESTATUS_OC:                            str = None,
    HOJA_CONTROL:                          str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ORDEN_DE_COMPRA",                       ORDEN_DE_COMPRA)
    af("FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO", FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO)
    af("SOLICITANTE",                           SOLICITANTE)
    af("PRESUPUESTO_MES",                       PRESUPUESTO_MES)
    af("SERIE_UBICACION_NO_EMPLEADO",           SERIE_UBICACION_NO_EMPLEADO)
    af("ACCESORIO_SOLICITADO",                  ACCESORIO_SOLICITADO)
    af("PROVEEDOR_ELEGIDO",                     PROVEEDOR_ELEGIDO)
    af("PIEZA_SERVICIO",                        PIEZA_SERVICIO)
    af("CANTIDAD",                              CANTIDAD)
    af("PRECIO_UNITARIO",                       PRECIO_UNITARIO)
    af("TOTAL_NO_INCLUYE_IVA",                  TOTAL_NO_INCLUYE_IVA)
    af("MONEDA",                                MONEDA)
    af("COMENTARIOS",                           COMENTARIOS)
    af("REQUISICION",                           REQUISICION)
    af("FECHA_OC",                              FECHA_OC)
    af("OC",                                    OC)
    af("FECHA_ENTRADA",                         FECHA_ENTRADA)
    af("CANTIDAD_REGISTRADA",                   CANTIDAD_REGISTRADA)
    af("ESTATUS_OC",                            ESTATUS_OC)
    af("HOJA_CONTROL",                          HOJA_CONTROL)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."ORDENES_DE_COMPRA" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f'''
        SELECT
            "ID","ORDEN_DE_COMPRA","FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO",
            "SOLICITANTE","PRESUPUESTO_MES","SERIE_UBICACION_NO_EMPLEADO",
            "ACCESORIO_SOLICITADO","PROVEEDOR_ELEGIDO","PIEZA_SERVICIO",
            "CANTIDAD","PRECIO_UNITARIO","TOTAL_NO_INCLUYE_IVA","MONEDA",
            "COMENTARIOS","REQUISICION","FECHA_OC","OC","FECHA_ENTRADA",
            "CANTIDAD_REGISTRADA","ESTATUS_OC","HOJA_CONTROL",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."ORDENES_DE_COMPRA"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    ''', params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- SUBIR PDF ----------------
@app.post("/ORDENES_DE_COMPRA/PDF/{id}")
async def subir_PDF_orden(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("ORDENES_DE_COMPRA", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."ORDENES_DE_COMPRA" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/ORDENES_DE_COMPRA/PDF/{id}")
def ver_PDF_orden(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."ORDENES_DE_COMPRA" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")


@app.delete("/ORDENES_DE_COMPRA/PDF/{id}")
def eliminar_PDF_orden(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."ORDENES_DE_COMPRA" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."ORDENES_DE_COMPRA" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}
# ---------------- EXPORTAR TODO ----------------
@app.get("/ORDENES_DE_COMPRA/EXPORTAR_TODO")
def exportar_todo_ordenes():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ORDEN_DE_COMPRA","FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO",
               "SOLICITANTE","PRESUPUESTO_MES","SERIE_UBICACION_NO_EMPLEADO",
               "ACCESORIO_SOLICITADO","PROVEEDOR_ELEGIDO","PIEZA_SERVICIO",
               "CANTIDAD","PRECIO_UNITARIO","TOTAL_NO_INCLUYE_IVA","MONEDA",
               "COMENTARIOS","REQUISICION","FECHA_OC","OC","FECHA_ENTRADA",
               "CANTIDAD_REGISTRADA","ESTATUS_OC","HOJA_CONTROL"
        FROM public."ORDENES_DE_COMPRA" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=ordenes_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/ORDENES_DE_COMPRA/EXPORTAR_ANIO")
def exportar_ordenes_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ORDEN_DE_COMPRA","FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO",
               "SOLICITANTE","PRESUPUESTO_MES","SERIE_UBICACION_NO_EMPLEADO",
               "ACCESORIO_SOLICITADO","PROVEEDOR_ELEGIDO","PIEZA_SERVICIO",
               "CANTIDAD","PRECIO_UNITARIO","TOTAL_NO_INCLUYE_IVA","MONEDA",
               "COMENTARIOS","REQUISICION","FECHA_OC","OC","FECHA_ENTRADA",
               "CANTIDAD_REGISTRADA","ESTATUS_OC","HOJA_CONTROL"
        FROM public."ORDENES_DE_COMPRA"
        WHERE EXTRACT(YEAR FROM "FECHA_OC") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=ordenes_{anio}.xlsx"}
    )

# ==========================================================
# =================== PANTALLAS NF =========================
# ==========================================================
# REEMPLAZA EL BLOQUE COMPLETO DE PANTALLAS NF EN backend.py

class PantallaNF(BaseModel):
    ID_UNICO: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NO_SERIE: Optional[str] = None
    CANTIDAD: Optional[str] = None
    TAMANO_PULGADAS: Optional[str] = None
    ACCESORIOS: Optional[str] = None
    MAC_WIFI: Optional[str] = None
    MAC_ETHERNET: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO_USD: Optional[str] = None
    VIDA_UTIL_MESES: Optional[str] = None
    ESTADO: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    ASIGNADO_A: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/PANTALLAS_NF")
def crear_pantalla(data: PantallaNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."PANTALLAS_NF"
        ("ID_UNICO","SUBCATEGORIA","MARCA","MODELO","NO_SERIE",
         "CANTIDAD","TAMANO_PULGADAS","ACCESORIOS",
         "MAC_WIFI","MAC_ETHERNET","PROVEEDOR",
         "COSTO_USD","VIDA_UTIL_MESES","ESTADO",
         "DISPONIBLE","FECHA_SALIDA","DESTINO_PLANTA",
         "ASIGNADO_A","PERSONAL_IT_ASIGNA")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE,
        data.CANTIDAD, data.TAMANO_PULGADAS, data.ACCESORIOS,
        data.MAC_WIFI, data.MAC_ETHERNET, data.PROVEEDOR,
        data.COSTO_USD, data.VIDA_UTIL_MESES, data.ESTADO,
        data.DISPONIBLE, data.FECHA_SALIDA, data.DESTINO_PLANTA,
        data.ASIGNADO_A, data.PERSONAL_IT_ASIGNA
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/PANTALLAS_NF")
def obtener_pantallas(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    SUBCATEGORIA: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NO_SERIE: str = None,
    CANTIDAD: str = None,
    TAMANO_PULGADAS: str = None,
    ACCESORIOS: str = None,
    MAC_WIFI: str = None,
    MAC_ETHERNET: str = None,
    PROVEEDOR: str = None,
    COSTO_USD: str = None,
    VIDA_UTIL_MESES: str = None,
    ESTADO: str = None,
    DISPONIBLE: str = None,
    FECHA_SALIDA: str = None,
    DESTINO_PLANTA: str = None,
    ASIGNADO_A: str = None,
    PERSONAL_IT_ASIGNA: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NO_SERIE", NO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("TAMANO_PULGADAS", TAMANO_PULGADAS)
    af("ACCESORIOS", ACCESORIOS)
    af("MAC_WIFI", MAC_WIFI)
    af("MAC_ETHERNET", MAC_ETHERNET)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO_USD", COSTO_USD)
    af("VIDA_UTIL_MESES", VIDA_UTIL_MESES)
    af("ESTADO", ESTADO)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."PANTALLAS_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","SUBCATEGORIA","MARCA","MODELO","NO_SERIE",
            "CANTIDAD","TAMANO_PULGADAS","ACCESORIOS",
            "MAC_WIFI","MAC_ETHERNET","PROVEEDOR",
            "COSTO_USD","VIDA_UTIL_MESES","ESTADO",
            "DISPONIBLE","FECHA_SALIDA","DESTINO_PLANTA",
            "ASIGNADO_A","PERSONAL_IT_ASIGNA",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."PANTALLAS_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/PANTALLAS_NF/{id}")
def editar_pantalla(id: int, data: PantallaNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."PANTALLAS_NF"
        SET
        "ID_UNICO"=%s,
        "SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,"NO_SERIE"=%s,
        "CANTIDAD"=%s,"TAMANO_PULGADAS"=%s,"ACCESORIOS"=%s,
        "MAC_WIFI"=%s,"MAC_ETHERNET"=%s,"PROVEEDOR"=%s,
        "COSTO_USD"=%s,"VIDA_UTIL_MESES"=%s,"ESTADO"=%s,
        "DISPONIBLE"=%s,"FECHA_SALIDA"=%s,"DESTINO_PLANTA"=%s,
        "ASIGNADO_A"=%s,"PERSONAL_IT_ASIGNA"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE,
        data.CANTIDAD, data.TAMANO_PULGADAS, data.ACCESORIOS,
        data.MAC_WIFI, data.MAC_ETHERNET, data.PROVEEDOR,
        data.COSTO_USD, data.VIDA_UTIL_MESES, data.ESTADO,
        data.DISPONIBLE, data.FECHA_SALIDA, data.DESTINO_PLANTA,
        data.ASIGNADO_A, data.PERSONAL_IT_ASIGNA, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/PANTALLAS_NF/{id}")
def eliminar_pantalla(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."PANTALLAS_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/PANTALLAS_NF/PDF/{id}")
async def subir_pdf_pantallas(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("PANTALLAS_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."PANTALLAS_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/PANTALLAS_NF/PDF/{id}")
def ver_pdf_pantallas(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."PANTALLAS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/PANTALLAS_NF/PDF/{id}")
def eliminar_pdf_pantallas(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."PANTALLAS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."PANTALLAS_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/PANTALLAS_NF/EXPORTAR")
def exportar_pantallas(
    ID_UNICO: str = None,
    SUBCATEGORIA: str = None, MARCA: str = None, MODELO: str = None,
    NO_SERIE: str = None, CANTIDAD: str = None, TAMANO_PULGADAS: str = None,
    ACCESORIOS: str = None, MAC_WIFI: str = None, MAC_ETHERNET: str = None,
    PROVEEDOR: str = None, COSTO_USD: str = None, VIDA_UTIL_MESES: str = None,
    ESTADO: str = None, DISPONIBLE: str = None, FECHA_SALIDA: str = None,
    DESTINO_PLANTA: str = None, ASIGNADO_A: str = None, PERSONAL_IT_ASIGNA: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NO_SERIE", NO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("TAMANO_PULGADAS", TAMANO_PULGADAS)
    af("ACCESORIOS", ACCESORIOS)
    af("MAC_WIFI", MAC_WIFI)
    af("MAC_ETHERNET", MAC_ETHERNET)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO_USD", COSTO_USD)
    af("VIDA_UTIL_MESES", VIDA_UTIL_MESES)
    af("ESTADO", ESTADO)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","SUBCATEGORIA","MARCA","MODELO","NO_SERIE",
               "CANTIDAD","TAMANO_PULGADAS","ACCESORIOS",
               "MAC_WIFI","MAC_ETHERNET","PROVEEDOR",
               "COSTO_USD","VIDA_UTIL_MESES","ESTADO",
               "DISPONIBLE","FECHA_SALIDA","DESTINO_PLANTA",
               "ASIGNADO_A","PERSONAL_IT_ASIGNA"
        FROM public."PANTALLAS_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=pantallas_filtradas.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/PANTALLAS_NF/EXPORTAR_TODO")
def exportar_todo_pantallas():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","SUBCATEGORIA","MARCA","MODELO","NO_SERIE",
               "CANTIDAD","TAMANO_PULGADAS","ACCESORIOS",
               "MAC_WIFI","MAC_ETHERNET","PROVEEDOR",
               "COSTO_USD","VIDA_UTIL_MESES","ESTADO",
               "DISPONIBLE","FECHA_SALIDA","DESTINO_PLANTA",
               "ASIGNADO_A","PERSONAL_IT_ASIGNA"
        FROM public."PANTALLAS_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=pantallas_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/PANTALLAS_NF/EXPORTAR_ANIO")
def exportar_pantallas_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","SUBCATEGORIA","MARCA","MODELO","NO_SERIE",
               "CANTIDAD","TAMANO_PULGADAS","ACCESORIOS",
               "MAC_WIFI","MAC_ETHERNET","PROVEEDOR",
               "COSTO_USD","VIDA_UTIL_MESES","ESTADO",
               "DISPONIBLE","FECHA_SALIDA","DESTINO_PLANTA",
               "ASIGNADO_A","PERSONAL_IT_ASIGNA"
        FROM public."PANTALLAS_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_SALIDA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=pantallas_{anio}.xlsx"}
    )

# ==========================================================
# =================== REFACCIONES_NF =========================
# ==========================================================


class RefaccionNF(BaseModel):
    OC: Optional[str] = None
    FOLIO_CORRECTIVO: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    SERIE: Optional[str] = None
    CANTIDAD: Optional[str] = None
    NUM_PARTE: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    COMENTARIOS: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/REFACCIONES_NF")
def crear_refaccion(data: RefaccionNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."REFACCIONES_NF"
        ("OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
         "SUBCATEGORIA","MARCA","MODELO","SERIE",
         "CANTIDAD","NUM_PARTE","COSTO","MONEDA",
         "PROVEEDOR","DISPONIBLE","COMENTARIOS")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.OC, data.FOLIO_CORRECTIVO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.SERIE,
        data.CANTIDAD, data.NUM_PARTE, data.COSTO, data.MONEDA,
        data.PROVEEDOR, data.DISPONIBLE, data.COMENTARIOS
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/REFACCIONES_NF")
def obtener_refacciones(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    OC: str = None,
    FOLIO_CORRECTIVO: str = None,
    FECHA_REGISTRO: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    MARCA: str = None,
    MODELO: str = None,
    SERIE: str = None,
    CANTIDAD: str = None,
    NUM_PARTE: str = None,
    COSTO: str = None,
    MONEDA: str = None,
    PROVEEDOR: str = None,
    DISPONIBLE: str = None,
    COMENTARIOS: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("OC", OC)
    af("FOLIO_CORRECTIVO", FOLIO_CORRECTIVO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("SERIE", SERIE)
    af("CANTIDAD", CANTIDAD)
    af("NUM_PARTE", NUM_PARTE)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("PROVEEDOR", PROVEEDOR)
    af("DISPONIBLE", DISPONIBLE)
    af("COMENTARIOS", COMENTARIOS)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."REFACCIONES_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
            "SUBCATEGORIA","MARCA","MODELO","SERIE","CANTIDAD",
            "NUM_PARTE","COSTO","MONEDA","PROVEEDOR","DISPONIBLE","COMENTARIOS",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."REFACCIONES_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/REFACCIONES_NF/{id}")
def editar_refaccion(id: int, data: RefaccionNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."REFACCIONES_NF"
        SET
        "OC"=%s,"FOLIO_CORRECTIVO"=%s,"FECHA_REGISTRO"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,"SERIE"=%s,
        "CANTIDAD"=%s,"NUM_PARTE"=%s,"COSTO"=%s,"MONEDA"=%s,
        "PROVEEDOR"=%s,"DISPONIBLE"=%s,"COMENTARIOS"=%s
        WHERE "ID"=%s
    """, (
        data.OC, data.FOLIO_CORRECTIVO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.SERIE,
        data.CANTIDAD, data.NUM_PARTE, data.COSTO, data.MONEDA,
        data.PROVEEDOR, data.DISPONIBLE, data.COMENTARIOS, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/REFACCIONES_NF/{id}")
def eliminar_refaccion(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."REFACCIONES_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/REFACCIONES_NF/PDF/{id}")
async def subir_pdf_refaccion(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("REFACCIONES_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."REFACCIONES_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/REFACCIONES_NF/PDF/{id}")
def ver_pdf_refaccion(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."REFACCIONES_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/REFACCIONES_NF/PDF/{id}")
def eliminar_pdf_refaccion(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."REFACCIONES_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."REFACCIONES_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/REFACCIONES_NF/EXPORTAR")
def exportar_refacciones(
    OC: str = None, FOLIO_CORRECTIVO: str = None, FECHA_REGISTRO: str = None,
    RECIBIDO_POR: str = None, SUBCATEGORIA: str = None, MARCA: str = None,
    MODELO: str = None, SERIE: str = None, CANTIDAD: str = None,
    NUM_PARTE: str = None, COSTO: str = None, MONEDA: str = None,
    PROVEEDOR: str = None, DISPONIBLE: str = None, COMENTARIOS: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("OC", OC)
    af("FOLIO_CORRECTIVO", FOLIO_CORRECTIVO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("SERIE", SERIE)
    af("CANTIDAD", CANTIDAD)
    af("NUM_PARTE", NUM_PARTE)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("PROVEEDOR", PROVEEDOR)
    af("DISPONIBLE", DISPONIBLE)
    af("COMENTARIOS", COMENTARIOS)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","SERIE","CANTIDAD",
               "NUM_PARTE","COSTO","MONEDA","PROVEEDOR","DISPONIBLE","COMENTARIOS"
        FROM public."REFACCIONES_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=refacciones_filtradas.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/REFACCIONES_NF/EXPORTAR_TODO")
def exportar_todo_refacciones():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","SERIE","CANTIDAD",
               "NUM_PARTE","COSTO","MONEDA","PROVEEDOR","DISPONIBLE","COMENTARIOS"
        FROM public."REFACCIONES_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=refacciones_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/REFACCIONES_NF/EXPORTAR_ANIO")
def exportar_refacciones_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","SERIE","CANTIDAD",
               "NUM_PARTE","COSTO","MONEDA","PROVEEDOR","DISPONIBLE","COMENTARIOS"
        FROM public."REFACCIONES_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=refacciones_{anio}.xlsx"}
    )


# ==========================================================
# =================== ACCESORIOS_NF ========================
# ==========================================================

class AccesorioNF(BaseModel):
    OC: Optional[str] = None
    FOLIO: Optional[str] = None
    FECHA_ENTRADA: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NO_SERIE: Optional[str] = None
    CANTIDAD: Optional[str] = None
    TIPO: Optional[str] = None
    ACCESORIOS: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    ASIGNADO_A: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/ACCESORIOS_NF")
def crear_accesorio(data: AccesorioNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."ACCESORIOS_NF"
        ("OC","FOLIO","FECHA_ENTRADA","RECIBIDO_POR",
         "SUBCATEGORIA","MARCA","MODELO","NO_SERIE",
         "CANTIDAD","TIPO","ACCESORIOS","PROVEEDOR",
         "COSTO","MONEDA","DISPONIBLE","FECHA_SALIDA",
         "ASIGNADO_A","DESTINO_PLANTA","PERSONAL_IT_ASIGNA")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.OC, data.FOLIO, data.FECHA_ENTRADA, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE,
        data.CANTIDAD, data.TIPO, data.ACCESORIOS, data.PROVEEDOR,
        data.COSTO, data.MONEDA, data.DISPONIBLE, data.FECHA_SALIDA,
        data.ASIGNADO_A, data.DESTINO_PLANTA, data.PERSONAL_IT_ASIGNA
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/ACCESORIOS_NF")
def obtener_accesorios(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    OC: str = None,
    FOLIO: str = None,
    FECHA_ENTRADA: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NO_SERIE: str = None,
    CANTIDAD: str = None,
    TIPO: str = None,
    ACCESORIOS: str = None,
    PROVEEDOR: str = None,
    COSTO: str = None,
    MONEDA: str = None,
    DISPONIBLE: str = None,
    FECHA_SALIDA: str = None,
    ASIGNADO_A: str = None,
    DESTINO_PLANTA: str = None,
    PERSONAL_IT_ASIGNA: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("OC", OC)
    af("FOLIO", FOLIO)
    af("FECHA_ENTRADA", FECHA_ENTRADA)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NO_SERIE", NO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("TIPO", TIPO)
    af("ACCESORIOS", ACCESORIOS)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."ACCESORIOS_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","OC","FOLIO","FECHA_ENTRADA","RECIBIDO_POR",
            "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
            "TIPO","ACCESORIOS","PROVEEDOR","COSTO","MONEDA",
            "DISPONIBLE","FECHA_SALIDA","ASIGNADO_A",
            "DESTINO_PLANTA","PERSONAL_IT_ASIGNA",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."ACCESORIOS_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/ACCESORIOS_NF/{id}")
def editar_accesorio(id: int, data: AccesorioNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."ACCESORIOS_NF"
        SET
        "OC"=%s,"FOLIO"=%s,"FECHA_ENTRADA"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,"NO_SERIE"=%s,
        "CANTIDAD"=%s,"TIPO"=%s,"ACCESORIOS"=%s,"PROVEEDOR"=%s,
        "COSTO"=%s,"MONEDA"=%s,"DISPONIBLE"=%s,"FECHA_SALIDA"=%s,
        "ASIGNADO_A"=%s,"DESTINO_PLANTA"=%s,"PERSONAL_IT_ASIGNA"=%s
        WHERE "ID"=%s
    """, (
        data.OC, data.FOLIO, data.FECHA_ENTRADA, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE,
        data.CANTIDAD, data.TIPO, data.ACCESORIOS, data.PROVEEDOR,
        data.COSTO, data.MONEDA, data.DISPONIBLE, data.FECHA_SALIDA,
        data.ASIGNADO_A, data.DESTINO_PLANTA, data.PERSONAL_IT_ASIGNA, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/ACCESORIOS_NF/{id}")
def eliminar_accesorio(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."ACCESORIOS_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/ACCESORIOS_NF/PDF/{id}")
async def subir_pdf_accesorio(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("ACCESORIOS_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."ACCESORIOS_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/ACCESORIOS_NF/PDF/{id}")
def ver_pdf_accesorio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."ACCESORIOS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/ACCESORIOS_NF/PDF/{id}")
def eliminar_pdf_accesorio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."ACCESORIOS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."ACCESORIOS_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/ACCESORIOS_NF/EXPORTAR")
def exportar_accesorios(
    OC: str = None, FOLIO: str = None, FECHA_ENTRADA: str = None,
    RECIBIDO_POR: str = None, SUBCATEGORIA: str = None, MARCA: str = None,
    MODELO: str = None, NO_SERIE: str = None, CANTIDAD: str = None,
    TIPO: str = None, ACCESORIOS: str = None, PROVEEDOR: str = None,
    COSTO: str = None, MONEDA: str = None, DISPONIBLE: str = None,
    FECHA_SALIDA: str = None, ASIGNADO_A: str = None,
    DESTINO_PLANTA: str = None, PERSONAL_IT_ASIGNA: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("OC", OC)
    af("FOLIO", FOLIO)
    af("FECHA_ENTRADA", FECHA_ENTRADA)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NO_SERIE", NO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("TIPO", TIPO)
    af("ACCESORIOS", ACCESORIOS)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","OC","FOLIO","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
               "TIPO","ACCESORIOS","PROVEEDOR","COSTO","MONEDA",
               "DISPONIBLE","FECHA_SALIDA","ASIGNADO_A",
               "DESTINO_PLANTA","PERSONAL_IT_ASIGNA"
        FROM public."ACCESORIOS_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=accesorios_filtrados.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/ACCESORIOS_NF/EXPORTAR_TODO")
def exportar_todo_accesorios():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","OC","FOLIO","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
               "TIPO","ACCESORIOS","PROVEEDOR","COSTO","MONEDA",
               "DISPONIBLE","FECHA_SALIDA","ASIGNADO_A",
               "DESTINO_PLANTA","PERSONAL_IT_ASIGNA"
        FROM public."ACCESORIOS_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=accesorios_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/ACCESORIOS_NF/EXPORTAR_ANIO")
def exportar_accesorios_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","OC","FOLIO","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
               "TIPO","ACCESORIOS","PROVEEDOR","COSTO","MONEDA",
               "DISPONIBLE","FECHA_SALIDA","ASIGNADO_A",
               "DESTINO_PLANTA","PERSONAL_IT_ASIGNA"
        FROM public."ACCESORIOS_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_ENTRADA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=accesorios_{anio}.xlsx"}
    )



# ==========================================================
# =================== DISPOSITIVOS_NF ======================
# ==========================================================
# REEMPLAZA EL BLOQUE COMPLETO DE DISPOSITIVOS_NF EN backend.py

class DispositivoNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NO_SERIE: Optional[str] = None
    CANTIDAD: Optional[str] = None
    COSTO: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    ACTIVO_FIJO: Optional[str] = None
    PROCESADOR: Optional[str] = None
    ARQUITECTURA: Optional[str] = None
    ALMACENAMIENTO: Optional[str] = None
    TIPO_DISCO: Optional[str] = None
    SISTEMA_OPERATIVO: Optional[str] = None
    LICENCIA_SO: Optional[str] = None
    MEMORIA_RAM: Optional[str] = None
    VELOCIDAD_MEMORIA: Optional[str] = None
    TIPO_MEMORIA: Optional[str] = None
    SLOT_MEMORIA: Optional[str] = None
    MAX_MEMORIA: Optional[str] = None
    MODELO_CARGADOR: Optional[str] = None
    NO_SERIE_ELIMINADOR: Optional[str] = None
    BATERIA_LAPTOP: Optional[str] = None
    WIFI_MAC: Optional[str] = None
    ETH_MAC: Optional[str] = None
    ACCESORIOS: Optional[str] = None
    UBICACION: Optional[str] = None
    EDIFICIO: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    ASIGNADO_A: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None
    FORMATO_BAJA: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/DISPOSITIVOS_NF")
def crear_dispositivo(data: DispositivoNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."DISPOSITIVOS_NF"
        ("ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR","SUBCATEGORIA",
         "MARCA","MODELO","NO_SERIE","CANTIDAD","COSTO","PROVEEDOR","ACTIVO_FIJO",
         "PROCESADOR","ARQUITECTURA","ALMACENAMIENTO","TIPO_DISCO","SISTEMA_OPERATIVO",
         "LICENCIA_SO","MEMORIA_RAM","VELOCIDAD_MEMORIA","TIPO_MEMORIA","SLOT_MEMORIA",
         "MAX_MEMORIA","MODELO_CARGADOR","NO_SERIE_ELIMINADOR","BATERIA_LAPTOP",
         "WIFI_MAC","ETH_MAC","ACCESORIOS","UBICACION","EDIFICIO",
         "FECHA_SALIDA","ASIGNADO_A","DESTINO_PLANTA","DISPONIBLE",
         "PERSONAL_IT_ASIGNA","FORMATO_BAJA")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE, data.CANTIDAD,
        data.COSTO, data.PROVEEDOR, data.ACTIVO_FIJO, data.PROCESADOR,
        data.ARQUITECTURA, data.ALMACENAMIENTO, data.TIPO_DISCO, data.SISTEMA_OPERATIVO,
        data.LICENCIA_SO, data.MEMORIA_RAM, data.VELOCIDAD_MEMORIA, data.TIPO_MEMORIA,
        data.SLOT_MEMORIA, data.MAX_MEMORIA, data.MODELO_CARGADOR,
        data.NO_SERIE_ELIMINADOR, data.BATERIA_LAPTOP, data.WIFI_MAC, data.ETH_MAC,
        data.ACCESORIOS, data.UBICACION, data.EDIFICIO, data.FECHA_SALIDA,
        data.ASIGNADO_A, data.DESTINO_PLANTA, data.DISPONIBLE,
        data.PERSONAL_IT_ASIGNA, data.FORMATO_BAJA
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/DISPOSITIVOS_NF")
def obtener_dispositivos(
    page: int = Query(1, ge=1), limit: int = Query(10, ge=1),
    ID_UNICO: str = None, OC: str = None, FOLIO: str = None,
    FECHA_REGISTRO: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    MARCA: str = None, MODELO: str = None, NO_SERIE: str = None,
    CANTIDAD: str = None, COSTO: str = None, PROVEEDOR: str = None,
    ACTIVO_FIJO: str = None, PROCESADOR: str = None, ARQUITECTURA: str = None,
    ALMACENAMIENTO: str = None, TIPO_DISCO: str = None, SISTEMA_OPERATIVO: str = None,
    LICENCIA_SO: str = None, MEMORIA_RAM: str = None, VELOCIDAD_MEMORIA: str = None,
    TIPO_MEMORIA: str = None, SLOT_MEMORIA: str = None, MAX_MEMORIA: str = None,
    MODELO_CARGADOR: str = None, NO_SERIE_ELIMINADOR: str = None,
    BATERIA_LAPTOP: str = None, WIFI_MAC: str = None, ETH_MAC: str = None,
    ACCESORIOS: str = None, UBICACION: str = None, EDIFICIO: str = None,
    FECHA_SALIDA: str = None, ASIGNADO_A: str = None, DESTINO_PLANTA: str = None,
    DISPONIBLE: str = None, PERSONAL_IT_ASIGNA: str = None, FORMATO_BAJA: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()
    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO",ID_UNICO); af("OC",OC); af("FOLIO",FOLIO)
    af("FECHA_REGISTRO",FECHA_REGISTRO); af("RECIBIDO_POR",RECIBIDO_POR)
    af("SUBCATEGORIA",SUBCATEGORIA); af("MARCA",MARCA); af("MODELO",MODELO)
    af("NO_SERIE",NO_SERIE); af("CANTIDAD",CANTIDAD); af("COSTO",COSTO)
    af("PROVEEDOR",PROVEEDOR); af("ACTIVO_FIJO",ACTIVO_FIJO)
    af("PROCESADOR",PROCESADOR); af("ARQUITECTURA",ARQUITECTURA)
    af("ALMACENAMIENTO",ALMACENAMIENTO); af("TIPO_DISCO",TIPO_DISCO)
    af("SISTEMA_OPERATIVO",SISTEMA_OPERATIVO); af("LICENCIA_SO",LICENCIA_SO)
    af("MEMORIA_RAM",MEMORIA_RAM); af("VELOCIDAD_MEMORIA",VELOCIDAD_MEMORIA)
    af("TIPO_MEMORIA",TIPO_MEMORIA); af("SLOT_MEMORIA",SLOT_MEMORIA)
    af("MAX_MEMORIA",MAX_MEMORIA); af("MODELO_CARGADOR",MODELO_CARGADOR)
    af("NO_SERIE_ELIMINADOR",NO_SERIE_ELIMINADOR); af("BATERIA_LAPTOP",BATERIA_LAPTOP)
    af("WIFI_MAC",WIFI_MAC); af("ETH_MAC",ETH_MAC); af("ACCESORIOS",ACCESORIOS)
    af("UBICACION",UBICACION); af("EDIFICIO",EDIFICIO); af("FECHA_SALIDA",FECHA_SALIDA)
    af("ASIGNADO_A",ASIGNADO_A); af("DESTINO_PLANTA",DESTINO_PLANTA)
    af("DISPONIBLE",DISPONIBLE); af("PERSONAL_IT_ASIGNA",PERSONAL_IT_ASIGNA)
    af("FORMATO_BAJA",FORMATO_BAJA)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""
    cursor.execute(f'SELECT COUNT(*) FROM public."DISPOSITIVOS_NF" {where_clause}', params)
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD","COSTO",
               "PROVEEDOR","ACTIVO_FIJO","PROCESADOR","ARQUITECTURA","ALMACENAMIENTO",
               "TIPO_DISCO","SISTEMA_OPERATIVO","LICENCIA_SO","MEMORIA_RAM",
               "VELOCIDAD_MEMORIA","TIPO_MEMORIA","SLOT_MEMORIA","MAX_MEMORIA",
               "MODELO_CARGADOR","NO_SERIE_ELIMINADOR","BATERIA_LAPTOP",
               "WIFI_MAC","ETH_MAC","ACCESORIOS","UBICACION","EDIFICIO",
               "FECHA_SALIDA","ASIGNADO_A","DESTINO_PLANTA","DISPONIBLE",
               "PERSONAL_IT_ASIGNA","FORMATO_BAJA",
               CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."DISPOSITIVOS_NF" {where_clause}
        ORDER BY "ID" DESC LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]
    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/DISPOSITIVOS_NF/{id}")
def editar_dispositivo(id: int, data: DispositivoNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."DISPOSITIVOS_NF" SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO"=%s,"FECHA_REGISTRO"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,"NO_SERIE"=%s,"CANTIDAD"=%s,
        "COSTO"=%s,"PROVEEDOR"=%s,"ACTIVO_FIJO"=%s,"PROCESADOR"=%s,
        "ARQUITECTURA"=%s,"ALMACENAMIENTO"=%s,"TIPO_DISCO"=%s,"SISTEMA_OPERATIVO"=%s,
        "LICENCIA_SO"=%s,"MEMORIA_RAM"=%s,"VELOCIDAD_MEMORIA"=%s,"TIPO_MEMORIA"=%s,
        "SLOT_MEMORIA"=%s,"MAX_MEMORIA"=%s,"MODELO_CARGADOR"=%s,
        "NO_SERIE_ELIMINADOR"=%s,"BATERIA_LAPTOP"=%s,"WIFI_MAC"=%s,"ETH_MAC"=%s,
        "ACCESORIOS"=%s,"UBICACION"=%s,"EDIFICIO"=%s,"FECHA_SALIDA"=%s,
        "ASIGNADO_A"=%s,"DESTINO_PLANTA"=%s,"DISPONIBLE"=%s,
        "PERSONAL_IT_ASIGNA"=%s,"FORMATO_BAJA"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE, data.CANTIDAD,
        data.COSTO, data.PROVEEDOR, data.ACTIVO_FIJO, data.PROCESADOR,
        data.ARQUITECTURA, data.ALMACENAMIENTO, data.TIPO_DISCO, data.SISTEMA_OPERATIVO,
        data.LICENCIA_SO, data.MEMORIA_RAM, data.VELOCIDAD_MEMORIA, data.TIPO_MEMORIA,
        data.SLOT_MEMORIA, data.MAX_MEMORIA, data.MODELO_CARGADOR,
        data.NO_SERIE_ELIMINADOR, data.BATERIA_LAPTOP, data.WIFI_MAC, data.ETH_MAC,
        data.ACCESORIOS, data.UBICACION, data.EDIFICIO, data.FECHA_SALIDA,
        data.ASIGNADO_A, data.DESTINO_PLANTA, data.DISPONIBLE,
        data.PERSONAL_IT_ASIGNA, data.FORMATO_BAJA, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/DISPOSITIVOS_NF/{id}")
def eliminar_dispositivo(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."DISPOSITIVOS_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/DISPOSITIVOS_NF/PDF/{id}")
async def subir_pdf_dispositivo(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("DISPOSITIVOS_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."DISPOSITIVOS_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/DISPOSITIVOS_NF/PDF/{id}")
def ver_pdf_dispositivo(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."DISPOSITIVOS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/DISPOSITIVOS_NF/PDF/{id}")
def eliminar_pdf_dispositivo(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."DISPOSITIVOS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."DISPOSITIVOS_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/DISPOSITIVOS_NF/EXPORTAR")
def exportar_dispositivos(
    ID_UNICO: str = None, OC: str = None, FOLIO: str = None,
    FECHA_REGISTRO: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    MARCA: str = None, MODELO: str = None, NO_SERIE: str = None,
    CANTIDAD: str = None, COSTO: str = None, PROVEEDOR: str = None,
    ACTIVO_FIJO: str = None, PROCESADOR: str = None, ARQUITECTURA: str = None,
    ALMACENAMIENTO: str = None, TIPO_DISCO: str = None, SISTEMA_OPERATIVO: str = None,
    LICENCIA_SO: str = None, MEMORIA_RAM: str = None, VELOCIDAD_MEMORIA: str = None,
    TIPO_MEMORIA: str = None, SLOT_MEMORIA: str = None, MAX_MEMORIA: str = None,
    MODELO_CARGADOR: str = None, NO_SERIE_ELIMINADOR: str = None,
    BATERIA_LAPTOP: str = None, WIFI_MAC: str = None, ETH_MAC: str = None,
    ACCESORIOS: str = None, UBICACION: str = None, EDIFICIO: str = None,
    FECHA_SALIDA: str = None, ASIGNADO_A: str = None, DESTINO_PLANTA: str = None,
    DISPONIBLE: str = None, PERSONAL_IT_ASIGNA: str = None, FORMATO_BAJA: str = None
):
    conn = get_connection()
    cursor = conn.cursor()
    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO",ID_UNICO); af("OC",OC); af("FOLIO",FOLIO)
    af("FECHA_REGISTRO",FECHA_REGISTRO); af("RECIBIDO_POR",RECIBIDO_POR)
    af("SUBCATEGORIA",SUBCATEGORIA); af("MARCA",MARCA); af("MODELO",MODELO)
    af("NO_SERIE",NO_SERIE); af("CANTIDAD",CANTIDAD); af("COSTO",COSTO)
    af("PROVEEDOR",PROVEEDOR); af("ACTIVO_FIJO",ACTIVO_FIJO)
    af("PROCESADOR",PROCESADOR); af("ARQUITECTURA",ARQUITECTURA)
    af("ALMACENAMIENTO",ALMACENAMIENTO); af("TIPO_DISCO",TIPO_DISCO)
    af("SISTEMA_OPERATIVO",SISTEMA_OPERATIVO); af("LICENCIA_SO",LICENCIA_SO)
    af("MEMORIA_RAM",MEMORIA_RAM); af("VELOCIDAD_MEMORIA",VELOCIDAD_MEMORIA)
    af("TIPO_MEMORIA",TIPO_MEMORIA); af("SLOT_MEMORIA",SLOT_MEMORIA)
    af("MAX_MEMORIA",MAX_MEMORIA); af("MODELO_CARGADOR",MODELO_CARGADOR)
    af("NO_SERIE_ELIMINADOR",NO_SERIE_ELIMINADOR); af("BATERIA_LAPTOP",BATERIA_LAPTOP)
    af("WIFI_MAC",WIFI_MAC); af("ETH_MAC",ETH_MAC); af("ACCESORIOS",ACCESORIOS)
    af("UBICACION",UBICACION); af("EDIFICIO",EDIFICIO); af("FECHA_SALIDA",FECHA_SALIDA)
    af("ASIGNADO_A",ASIGNADO_A); af("DESTINO_PLANTA",DESTINO_PLANTA)
    af("DISPONIBLE",DISPONIBLE); af("PERSONAL_IT_ASIGNA",PERSONAL_IT_ASIGNA)
    af("FORMATO_BAJA",FORMATO_BAJA)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""
    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD","COSTO",
               "PROVEEDOR","ACTIVO_FIJO","PROCESADOR","ARQUITECTURA","ALMACENAMIENTO",
               "TIPO_DISCO","SISTEMA_OPERATIVO","LICENCIA_SO","MEMORIA_RAM",
               "VELOCIDAD_MEMORIA","TIPO_MEMORIA","SLOT_MEMORIA","MAX_MEMORIA",
               "MODELO_CARGADOR","NO_SERIE_ELIMINADOR","BATERIA_LAPTOP",
               "WIFI_MAC","ETH_MAC","ACCESORIOS","UBICACION","EDIFICIO",
               "FECHA_SALIDA","ASIGNADO_A","DESTINO_PLANTA","DISPONIBLE",
               "PERSONAL_IT_ASIGNA","FORMATO_BAJA"
        FROM public."DISPOSITIVOS_NF" {where} ORDER BY "ID" DESC
    """, params)
    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=dispositivos_filtrados.xlsx"})

# ---------------- EXPORTAR TODO ----------------
@app.get("/DISPOSITIVOS_NF/EXPORTAR_TODO")
def exportar_todo_dispositivos():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD","COSTO",
               "PROVEEDOR","ACTIVO_FIJO","PROCESADOR","ARQUITECTURA","ALMACENAMIENTO",
               "TIPO_DISCO","SISTEMA_OPERATIVO","LICENCIA_SO","MEMORIA_RAM",
               "VELOCIDAD_MEMORIA","TIPO_MEMORIA","SLOT_MEMORIA","MAX_MEMORIA",
               "MODELO_CARGADOR","NO_SERIE_ELIMINADOR","BATERIA_LAPTOP",
               "WIFI_MAC","ETH_MAC","ACCESORIOS","UBICACION","EDIFICIO",
               "FECHA_SALIDA","ASIGNADO_A","DESTINO_PLANTA","DISPONIBLE",
               "PERSONAL_IT_ASIGNA","FORMATO_BAJA"
        FROM public."DISPOSITIVOS_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=dispositivos_todo.xlsx"})

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/DISPOSITIVOS_NF/EXPORTAR_ANIO")
def exportar_dispositivos_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD","COSTO",
               "PROVEEDOR","ACTIVO_FIJO","PROCESADOR","ARQUITECTURA","ALMACENAMIENTO",
               "TIPO_DISCO","SISTEMA_OPERATIVO","LICENCIA_SO","MEMORIA_RAM",
               "VELOCIDAD_MEMORIA","TIPO_MEMORIA","SLOT_MEMORIA","MAX_MEMORIA",
               "MODELO_CARGADOR","NO_SERIE_ELIMINADOR","BATERIA_LAPTOP",
               "WIFI_MAC","ETH_MAC","ACCESORIOS","UBICACION","EDIFICIO",
               "FECHA_SALIDA","ASIGNADO_A","DESTINO_PLANTA","DISPONIBLE",
               "PERSONAL_IT_ASIGNA","FORMATO_BAJA"
        FROM public."DISPOSITIVOS_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=dispositivos_{anio}.xlsx"})




# ==========================================================
# ===================== INVENTARIOS ========================
# ==========================================================
# REEMPLAZA EL BLOQUE COMPLETO DE INVENTARIOS EN backend.py

class Inventario(BaseModel):
    INV_FOLIO: Optional[str] = None
    EQUIPO: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    CANTIDAD: Optional[str] = None
    PRECIO_UNITARIO: Optional[float] = None
    MONEDA: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    PRESUPUESTO: Optional[str] = None
    STATUS: Optional[str] = None
    ANO: Optional[str] = None
    OC: Optional[str] = None
    NUMERO_SERIE: Optional[str] = None
    UBICACION_ACTUAL: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/INVENTARIOS")
def crear_inventario(data: Inventario):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."INVENTARIOS"
        ("INV_FOLIO","EQUIPO","MARCA","MODELO","CANTIDAD",
         "PRECIO_UNITARIO","MONEDA","PROVEEDOR","PRESUPUESTO",
         "STATUS","ANO","OC","NUMERO_SERIE","UBICACION_ACTUAL")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.INV_FOLIO, data.EQUIPO, data.MARCA, data.MODELO, data.CANTIDAD,
        data.PRECIO_UNITARIO, data.MONEDA, data.PROVEEDOR, data.PRESUPUESTO,
        data.STATUS, data.ANO, data.OC, data.NUMERO_SERIE, data.UBICACION_ACTUAL
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/INVENTARIOS")
def obtener_inventarios(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    INV_FOLIO: str = None,
    EQUIPO: str = None,
    MARCA: str = None,
    MODELO: str = None,
    CANTIDAD: str = None,
    PRECIO_UNITARIO: str = None,
    PRECIO_CON_IVA: str = None,
    MONEDA: str = None,
    PROVEEDOR: str = None,
    PRESUPUESTO: str = None,
    STATUS: str = None,
    ANO: str = None,
    OC: str = None,
    NUMERO_SERIE: str = None,
    UBICACION_ACTUAL: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("INV_FOLIO", INV_FOLIO)
    af("EQUIPO", EQUIPO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("CANTIDAD", CANTIDAD)
    af("PRECIO_UNITARIO", PRECIO_UNITARIO)
    af("PRECIO_CON_IVA", PRECIO_CON_IVA)
    af("MONEDA", MONEDA)
    af("PROVEEDOR", PROVEEDOR)
    af("PRESUPUESTO", PRESUPUESTO)
    af("STATUS", STATUS)
    af("ANO", ANO)
    af("OC", OC)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("UBICACION_ACTUAL", UBICACION_ACTUAL)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."INVENTARIOS" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","INV_FOLIO","EQUIPO","MARCA","MODELO","CANTIDAD",
            "PRECIO_UNITARIO","PRECIO_CON_IVA","MONEDA","PROVEEDOR","PRESUPUESTO",
            "STATUS","ANO","OC","NUMERO_SERIE","UBICACION_ACTUAL",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."INVENTARIOS"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/INVENTARIOS/{id}")
def editar_inventario(id: int, data: Inventario):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."INVENTARIOS"
        SET
        "INV_FOLIO"=%s,"EQUIPO"=%s,"MARCA"=%s,"MODELO"=%s,"CANTIDAD"=%s,
        "PRECIO_UNITARIO"=%s,"MONEDA"=%s,"PROVEEDOR"=%s,"PRESUPUESTO"=%s,
        "STATUS"=%s,"ANO"=%s,"OC"=%s,"NUMERO_SERIE"=%s,"UBICACION_ACTUAL"=%s
        WHERE "ID"=%s
    """, (
        data.INV_FOLIO, data.EQUIPO, data.MARCA, data.MODELO, data.CANTIDAD,
        data.PRECIO_UNITARIO, data.MONEDA, data.PROVEEDOR, data.PRESUPUESTO,
        data.STATUS, data.ANO, data.OC, data.NUMERO_SERIE, data.UBICACION_ACTUAL, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/INVENTARIOS/{id}")
def eliminar_inventario(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."INVENTARIOS" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/INVENTARIOS/PDF/{id}")
async def subir_pdf_inventario(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("INVENTARIOS", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."INVENTARIOS" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/INVENTARIOS/PDF/{id}")
def ver_pdf_inventario(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."INVENTARIOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/INVENTARIOS/PDF/{id}")
def eliminar_pdf_inventario(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."INVENTARIOS" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."INVENTARIOS" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/INVENTARIOS/EXPORTAR")
def exportar_inventarios(
    INV_FOLIO: str = None, EQUIPO: str = None, MARCA: str = None,
    MODELO: str = None, CANTIDAD: str = None, PRECIO_UNITARIO: str = None,
    PRECIO_CON_IVA: str = None, MONEDA: str = None, PROVEEDOR: str = None, PRESUPUESTO: str = None,
    STATUS: str = None, ANO: str = None, OC: str = None,
    NUMERO_SERIE: str = None, UBICACION_ACTUAL: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("INV_FOLIO", INV_FOLIO)
    af("EQUIPO", EQUIPO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("CANTIDAD", CANTIDAD)
    af("PRECIO_UNITARIO", PRECIO_UNITARIO)
    af("PRECIO_CON_IVA", PRECIO_CON_IVA)
    af("MONEDA", MONEDA)
    af("PROVEEDOR", PROVEEDOR)
    af("PRESUPUESTO", PRESUPUESTO)
    af("STATUS", STATUS)
    af("ANO", ANO)
    af("OC", OC)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("UBICACION_ACTUAL", UBICACION_ACTUAL)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","INV_FOLIO","EQUIPO","MARCA","MODELO","CANTIDAD",
               "PRECIO_UNITARIO","PRECIO_CON_IVA","MONEDA","PROVEEDOR","PRESUPUESTO",
               "STATUS","ANO","OC","NUMERO_SERIE","UBICACION_ACTUAL"
        FROM public."INVENTARIOS" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=inventarios_filtrados.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/INVENTARIOS/EXPORTAR_TODO")
def exportar_todo_inventarios():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","INV_FOLIO","EQUIPO","MARCA","MODELO","CANTIDAD",
               "PRECIO_UNITARIO","PRECIO_CON_IVA","MONEDA","PROVEEDOR","PRESUPUESTO",
               "STATUS","ANO","OC","NUMERO_SERIE","UBICACION_ACTUAL"
        FROM public."INVENTARIOS" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=inventarios_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/INVENTARIOS/EXPORTAR_ANIO")
def exportar_inventarios_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","INV_FOLIO","EQUIPO","MARCA","MODELO","CANTIDAD",
               "PRECIO_UNITARIO","PRECIO_CON_IVA","MONEDA","PROVEEDOR","PRESUPUESTO",
               "STATUS","ANO","OC","NUMERO_SERIE","UBICACION_ACTUAL"
        FROM public."INVENTARIOS"
        WHERE "ANO" = %s
        ORDER BY "ID" DESC
    """, conn, params=(str(anio),))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=inventarios_{anio}.xlsx"}
    )


# ==========================================================
# =================== CAMARAS_DE_AUDIO =====================
# ==========================================================


class CamaraAudio(BaseModel):
    OC: Optional[str] = None
    FOLIO_INVENTARIO: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    TIPO: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NUMERO_SERIE: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    CANTIDAD: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    DESTINO: Optional[str] = None
    ACCESORIOS: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    PLANTA: Optional[str] = None
    DESTINO2: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None
    FOLIO_SERVICIO: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/CAMARAS_DE_AUDIO")
def crear_camara(data: CamaraAudio):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."CAMARAS_DE_AUDIO"
        ("OC","FOLIO_INVENTARIO","FECHA_REGISTRO","RECIBIDO_POR",
         "SUBCATEGORIA","TIPO","MARCA","MODELO",
         "NUMERO_SERIE","PROVEEDOR","CANTIDAD",
         "COSTO","MONEDA","DESTINO","ACCESORIOS",
         "FECHA_SALIDA","PLANTA","DESTINO2",
         "PERSONAL_IT_ASIGNA","FOLIO_SERVICIO")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.OC, data.FOLIO_INVENTARIO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.TIPO, data.MARCA, data.MODELO,
        data.NUMERO_SERIE, data.PROVEEDOR, data.CANTIDAD,
        data.COSTO, data.MONEDA, data.DESTINO, data.ACCESORIOS,
        data.FECHA_SALIDA, data.PLANTA, data.DESTINO2,
        data.PERSONAL_IT_ASIGNA, data.FOLIO_SERVICIO
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/CAMARAS_DE_AUDIO")
def obtener_camaras(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    OC: str = None,
    FOLIO_INVENTARIO: str = None,
    FECHA_REGISTRO: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    TIPO: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NUMERO_SERIE: str = None,
    PROVEEDOR: str = None,
    CANTIDAD: str = None,
    COSTO: str = None,
    MONEDA: str = None,
    DESTINO: str = None,
    ACCESORIOS: str = None,
    FECHA_SALIDA: str = None,
    PLANTA: str = None,
    DESTINO2: str = None,
    PERSONAL_IT_ASIGNA: str = None,
    FOLIO_SERVICIO: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("OC", OC)
    af("FOLIO_INVENTARIO", FOLIO_INVENTARIO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("TIPO", TIPO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("PROVEEDOR", PROVEEDOR)
    af("CANTIDAD", CANTIDAD)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("DESTINO", DESTINO)
    af("ACCESORIOS", ACCESORIOS)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("PLANTA", PLANTA)
    af("DESTINO2", DESTINO2)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)
    af("FOLIO_SERVICIO", FOLIO_SERVICIO)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."CAMARAS_DE_AUDIO" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","OC","FOLIO_INVENTARIO","FECHA_REGISTRO","RECIBIDO_POR",
            "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_SERIE",
            "PROVEEDOR","CANTIDAD","COSTO","MONEDA","DESTINO","ACCESORIOS",
            "FECHA_SALIDA","PLANTA","DESTINO2","PERSONAL_IT_ASIGNA","FOLIO_SERVICIO",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."CAMARAS_DE_AUDIO"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/CAMARAS_DE_AUDIO/{id}")
def editar_camara(id: int, data: CamaraAudio):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."CAMARAS_DE_AUDIO"
        SET
        "OC"=%s,"FOLIO_INVENTARIO"=%s,"FECHA_REGISTRO"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"TIPO"=%s,"MARCA"=%s,"MODELO"=%s,
        "NUMERO_SERIE"=%s,"PROVEEDOR"=%s,"CANTIDAD"=%s,
        "COSTO"=%s,"MONEDA"=%s,"DESTINO"=%s,"ACCESORIOS"=%s,
        "FECHA_SALIDA"=%s,"PLANTA"=%s,"DESTINO2"=%s,
        "PERSONAL_IT_ASIGNA"=%s,"FOLIO_SERVICIO"=%s
        WHERE "ID"=%s
    """, (
        data.OC, data.FOLIO_INVENTARIO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.TIPO, data.MARCA, data.MODELO,
        data.NUMERO_SERIE, data.PROVEEDOR, data.CANTIDAD,
        data.COSTO, data.MONEDA, data.DESTINO, data.ACCESORIOS,
        data.FECHA_SALIDA, data.PLANTA, data.DESTINO2,
        data.PERSONAL_IT_ASIGNA, data.FOLIO_SERVICIO, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/CAMARAS_DE_AUDIO/{id}")
def eliminar_camara(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."CAMARAS_DE_AUDIO" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/CAMARAS_DE_AUDIO/PDF/{id}")
async def subir_pdf_camara(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("CAMARAS_DE_AUDIO", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."CAMARAS_DE_AUDIO" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/CAMARAS_DE_AUDIO/PDF/{id}")
def ver_pdf_camara(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."CAMARAS_DE_AUDIO" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/CAMARAS_DE_AUDIO/PDF/{id}")
def eliminar_pdf_camara(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."CAMARAS_DE_AUDIO" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."CAMARAS_DE_AUDIO" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/CAMARAS_DE_AUDIO/EXPORTAR")
def exportar_camaras(
    OC: str = None, FOLIO_INVENTARIO: str = None, FECHA_REGISTRO: str = None,
    RECIBIDO_POR: str = None, SUBCATEGORIA: str = None, TIPO: str = None,
    MARCA: str = None, MODELO: str = None, NUMERO_SERIE: str = None,
    PROVEEDOR: str = None, CANTIDAD: str = None, COSTO: str = None,
    MONEDA: str = None, DESTINO: str = None, ACCESORIOS: str = None,
    FECHA_SALIDA: str = None, PLANTA: str = None, DESTINO2: str = None,
    PERSONAL_IT_ASIGNA: str = None, FOLIO_SERVICIO: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("OC", OC)
    af("FOLIO_INVENTARIO", FOLIO_INVENTARIO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("TIPO", TIPO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("PROVEEDOR", PROVEEDOR)
    af("CANTIDAD", CANTIDAD)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("DESTINO", DESTINO)
    af("ACCESORIOS", ACCESORIOS)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("PLANTA", PLANTA)
    af("DESTINO2", DESTINO2)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)
    af("FOLIO_SERVICIO", FOLIO_SERVICIO)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","OC","FOLIO_INVENTARIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_SERIE",
               "PROVEEDOR","CANTIDAD","COSTO","MONEDA","DESTINO","ACCESORIOS",
               "FECHA_SALIDA","PLANTA","DESTINO2","PERSONAL_IT_ASIGNA","FOLIO_SERVICIO"
        FROM public."CAMARAS_DE_AUDIO" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=camaras_filtradas.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/CAMARAS_DE_AUDIO/EXPORTAR_TODO")
def exportar_todo_camaras():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","OC","FOLIO_INVENTARIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_SERIE",
               "PROVEEDOR","CANTIDAD","COSTO","MONEDA","DESTINO","ACCESORIOS",
               "FECHA_SALIDA","PLANTA","DESTINO2","PERSONAL_IT_ASIGNA","FOLIO_SERVICIO"
        FROM public."CAMARAS_DE_AUDIO" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=camaras_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/CAMARAS_DE_AUDIO/EXPORTAR_ANIO")
def exportar_camaras_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","OC","FOLIO_INVENTARIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_SERIE",
               "PROVEEDOR","CANTIDAD","COSTO","MONEDA","DESTINO","ACCESORIOS",
               "FECHA_SALIDA","PLANTA","DESTINO2","PERSONAL_IT_ASIGNA","FOLIO_SERVICIO"
        FROM public."CAMARAS_DE_AUDIO"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=camaras_{anio}.xlsx"}
    )
    

# ==========================================================
# =================== PERIFERICOS_NF =======================
# ==========================================================
# REEMPLAZA EL BLOQUE COMPLETO DE PERIFERICOS_NF EN backend.py

class Periferico(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO_INVENTARIO: Optional[str] = None
    FECHA_ENTRADA: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    TIPO: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NUMERO_DE_SERIE: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO_PESOS: Optional[float] = None
    ESTADO: Optional[str] = None
    DESTINO: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    ASIGNADO_A: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/PERIFERICOS_NF")
def crear_periferico(data: Periferico):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."PERIFERICOS_NF"
        ("ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA",
         "RECIBIDO_POR","SUBCATEGORIA","TIPO","MARCA","MODELO",
         "NUMERO_DE_SERIE","PROVEEDOR","COSTO_PESOS",
         "ESTADO","DESTINO","DISPONIBLE",
         "FECHA_SALIDA","DESTINO_PLANTA",
         "ASIGNADO_A","PERSONAL_IT_ASIGNA")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_INVENTARIO, data.FECHA_ENTRADA, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.TIPO, data.MARCA, data.MODELO,
        data.NUMERO_DE_SERIE, data.PROVEEDOR, data.COSTO_PESOS,
        data.ESTADO, data.DESTINO, data.DISPONIBLE,
        data.FECHA_SALIDA, data.DESTINO_PLANTA,
        data.ASIGNADO_A, data.PERSONAL_IT_ASIGNA
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/PERIFERICOS_NF")
def obtener_perifericos(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    OC: str = None,
    FOLIO_INVENTARIO: str = None,
    FECHA_ENTRADA: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    TIPO: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NUMERO_DE_SERIE: str = None,
    PROVEEDOR: str = None,
    COSTO_PESOS: str = None,
    ESTADO: str = None,
    DESTINO: str = None,
    DISPONIBLE: str = None,
    FECHA_SALIDA: str = None,
    DESTINO_PLANTA: str = None,
    ASIGNADO_A: str = None,
    PERSONAL_IT_ASIGNA: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO_INVENTARIO", FOLIO_INVENTARIO)
    af("FECHA_ENTRADA", FECHA_ENTRADA)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("TIPO", TIPO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_DE_SERIE", NUMERO_DE_SERIE)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO_PESOS", COSTO_PESOS)
    af("ESTADO", ESTADO)
    af("DESTINO", DESTINO)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."PERIFERICOS_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
            "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_DE_SERIE",
            "PROVEEDOR","COSTO_PESOS","ESTADO","DESTINO","DISPONIBLE",
            "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A","PERSONAL_IT_ASIGNA",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."PERIFERICOS_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/PERIFERICOS_NF/{id}")
def editar_periferico(id: int, data: Periferico):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."PERIFERICOS_NF"
        SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO_INVENTARIO"=%s,"FECHA_ENTRADA"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"TIPO"=%s,"MARCA"=%s,"MODELO"=%s,
        "NUMERO_DE_SERIE"=%s,"PROVEEDOR"=%s,"COSTO_PESOS"=%s,
        "ESTADO"=%s,"DESTINO"=%s,"DISPONIBLE"=%s,
        "FECHA_SALIDA"=%s,"DESTINO_PLANTA"=%s,
        "ASIGNADO_A"=%s,"PERSONAL_IT_ASIGNA"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_INVENTARIO, data.FECHA_ENTRADA, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.TIPO, data.MARCA, data.MODELO,
        data.NUMERO_DE_SERIE, data.PROVEEDOR, data.COSTO_PESOS,
        data.ESTADO, data.DESTINO, data.DISPONIBLE,
        data.FECHA_SALIDA, data.DESTINO_PLANTA,
        data.ASIGNADO_A, data.PERSONAL_IT_ASIGNA, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/PERIFERICOS_NF/{id}")
def eliminar_periferico(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."PERIFERICOS_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/PERIFERICOS_NF/PDF/{id}")
async def subir_pdf_periferico(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("PERIFERICOS_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."PERIFERICOS_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/PERIFERICOS_NF/PDF/{id}")
def ver_pdf_periferico(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."PERIFERICOS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/PERIFERICOS_NF/PDF/{id}")
def eliminar_pdf_periferico(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."PERIFERICOS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."PERIFERICOS_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/PERIFERICOS_NF/EXPORTAR")
def exportar_perifericos(
    ID_UNICO: str = None, OC: str = None, FOLIO_INVENTARIO: str = None, FECHA_ENTRADA: str = None,
    RECIBIDO_POR: str = None, SUBCATEGORIA: str = None, TIPO: str = None,
    MARCA: str = None, MODELO: str = None, NUMERO_DE_SERIE: str = None,
    PROVEEDOR: str = None, COSTO_PESOS: str = None, ESTADO: str = None,
    DESTINO: str = None, DISPONIBLE: str = None, FECHA_SALIDA: str = None,
    DESTINO_PLANTA: str = None, ASIGNADO_A: str = None,
    PERSONAL_IT_ASIGNA: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO_INVENTARIO", FOLIO_INVENTARIO)
    af("FECHA_ENTRADA", FECHA_ENTRADA)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("TIPO", TIPO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_DE_SERIE", NUMERO_DE_SERIE)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO_PESOS", COSTO_PESOS)
    af("ESTADO", ESTADO)
    af("DESTINO", DESTINO)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_DE_SERIE",
               "PROVEEDOR","COSTO_PESOS","ESTADO","DESTINO","DISPONIBLE",
               "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A","PERSONAL_IT_ASIGNA"
        FROM public."PERIFERICOS_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=perifericos_filtrados.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/PERIFERICOS_NF/EXPORTAR_TODO")
def exportar_todo_perifericos():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_DE_SERIE",
               "PROVEEDOR","COSTO_PESOS","ESTADO","DESTINO","DISPONIBLE",
               "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A","PERSONAL_IT_ASIGNA"
        FROM public."PERIFERICOS_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=perifericos_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/PERIFERICOS_NF/EXPORTAR_ANIO")
def exportar_perifericos_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO","MARCA","MODELO","NUMERO_DE_SERIE",
               "PROVEEDOR","COSTO_PESOS","ESTADO","DESTINO","DISPONIBLE",
               "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A","PERSONAL_IT_ASIGNA"
        FROM public."PERIFERICOS_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_ENTRADA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=perifericos_{anio}.xlsx"}
    )

# ==========================================================
# =================== HERRAMIENTA_NF =======================
# ==========================================================

class HerramientaNF(BaseModel):
    ID_UNICO: int

    OC: Optional[str] = None
    FOLIO_CORRECTIVO: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    TIPO_USO: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    CANTIDAD: Optional[int] = None
    NUMERO_SERIE: Optional[str] = None
    NUM_PARTE: Optional[str] = None
    COSTO: Optional[float] = None
    MONEDA: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    UBICACION: Optional[str] = None
    COMENTARIOS: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/HERRAMIENTA_NF")
def crear_herramienta_nf(data: HerramientaNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."HERRAMIENTA_NF"
        ("ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
         "SUBCATEGORIA","TIPO_USO","MARCA","MODELO","CANTIDAD",
         "NUMERO_SERIE","NUM_PARTE","COSTO","MONEDA",
         "PROVEEDOR","UBICACION","COMENTARIOS")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_CORRECTIVO, data.FECHA_REGISTRO,
        data.RECIBIDO_POR, data.SUBCATEGORIA, data.TIPO_USO, data.MARCA,
        data.MODELO, data.CANTIDAD, data.NUMERO_SERIE, data.NUM_PARTE,
        data.COSTO, data.MONEDA, data.PROVEEDOR, data.UBICACION, data.COMENTARIOS
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/HERRAMIENTAS_NF")
def obtener_herramientas_nf(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    OC: str = None,
    FOLIO_CORRECTIVO: str = None,
    FECHA_REGISTRO: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    TIPO_USO: str = None,
    MARCA: str = None,
    MODELO: str = None,
    CANTIDAD: str = None,
    NUMERO_SERIE: str = None,
    NUM_PARTE: str = None,
    COSTO: str = None,
    MONEDA: str = None,
    PROVEEDOR: str = None,
    UBICACION: str = None,
    COMENTARIOS: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO_CORRECTIVO", FOLIO_CORRECTIVO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("TIPO_USO", TIPO_USO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("CANTIDAD", CANTIDAD)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("NUM_PARTE", NUM_PARTE)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("PROVEEDOR", PROVEEDOR)
    af("UBICACION", UBICACION)
    af("COMENTARIOS", COMENTARIOS)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."HERRAMIENTA_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
            "SUBCATEGORIA","TIPO_USO","MARCA","MODELO","CANTIDAD",
            "NUMERO_SERIE","NUM_PARTE","COSTO","MONEDA",
            "PROVEEDOR","UBICACION","COMENTARIOS",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."HERRAMIENTA_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/HERRAMIENTA_NF/{id}")
def editar_herramienta_nf(id: int, data: HerramientaNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."HERRAMIENTA_NF"
        SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO_CORRECTIVO"=%s,"FECHA_REGISTRO"=%s,
        "RECIBIDO_POR"=%s,"SUBCATEGORIA"=%s,"TIPO_USO"=%s,"MARCA"=%s,
        "MODELO"=%s,"CANTIDAD"=%s,"NUMERO_SERIE"=%s,"NUM_PARTE"=%s,
        "COSTO"=%s,"MONEDA"=%s,"PROVEEDOR"=%s,"UBICACION"=%s,"COMENTARIOS"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_CORRECTIVO, data.FECHA_REGISTRO,
        data.RECIBIDO_POR, data.SUBCATEGORIA, data.TIPO_USO, data.MARCA,
        data.MODELO, data.CANTIDAD, data.NUMERO_SERIE, data.NUM_PARTE,
        data.COSTO, data.MONEDA, data.PROVEEDOR, data.UBICACION, data.COMENTARIOS, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/HERRAMIENTA_NF/{id}")
def eliminar_herramienta_nf(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."HERRAMIENTA_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/HERRAMIENTA_NF/PDF/{id}")
async def subir_pdf_herramienta(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("HERRAMIENTA_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."HERRAMIENTA_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/HERRAMIENTA_NF/PDF/{id}")
def ver_pdf_herramienta(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."HERRAMIENTA_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/HERRAMIENTA_NF/PDF/{id}")
def eliminar_pdf_herramienta(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."HERRAMIENTA_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."HERRAMIENTA_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/HERRAMIENTA_NF/EXPORTAR")
def exportar_herramientas(
    ID_UNICO: str = None, OC: str = None, FOLIO_CORRECTIVO: str = None,
    FECHA_REGISTRO: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    TIPO_USO: str = None, MARCA: str = None, MODELO: str = None,
    CANTIDAD: str = None, NUMERO_SERIE: str = None, NUM_PARTE: str = None,
    COSTO: str = None, MONEDA: str = None, PROVEEDOR: str = None,
    UBICACION: str = None, COMENTARIOS: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO_CORRECTIVO", FOLIO_CORRECTIVO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("TIPO_USO", TIPO_USO)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("CANTIDAD", CANTIDAD)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("NUM_PARTE", NUM_PARTE)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("PROVEEDOR", PROVEEDOR)
    af("UBICACION", UBICACION)
    af("COMENTARIOS", COMENTARIOS)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO_USO","MARCA","MODELO","CANTIDAD",
               "NUMERO_SERIE","NUM_PARTE","COSTO","MONEDA",
               "PROVEEDOR","UBICACION","COMENTARIOS"
        FROM public."HERRAMIENTA_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=herramientas_filtradas.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/HERRAMIENTA_NF/EXPORTAR_TODO")
def exportar_todo_herramientas():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO_USO","MARCA","MODELO","CANTIDAD",
               "NUMERO_SERIE","NUM_PARTE","COSTO","MONEDA",
               "PROVEEDOR","UBICACION","COMENTARIOS"
        FROM public."HERRAMIENTA_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=herramientas_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/HERRAMIENTA_NF/EXPORTAR_ANIO")
def exportar_herramientas_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","TIPO_USO","MARCA","MODELO","CANTIDAD",
               "NUMERO_SERIE","NUM_PARTE","COSTO","MONEDA",
               "PROVEEDOR","UBICACION","COMENTARIOS"
        FROM public."HERRAMIENTA_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=herramientas_{anio}.xlsx"}
    )

# ==========================================================
# =================== IMPRESORAS_NF ========================
# ==========================================================


class ImpresoraNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO_INVENTARIO: Optional[str] = None
    FECHA_ENTRADA: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NUMERO_SERIE: Optional[str] = None
    TIPO: Optional[str] = None
    CANTIDAD: Optional[str] = None
    IP: Optional[str] = None
    MAC: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    UBICACION: Optional[str] = None
    ESTADO: Optional[str] = None
    PLANTA: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    FECHA_ASIGNACION: Optional[str] = None
    OBSERVACIONES: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    ASIGNADO_A: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None
    FECHA_MANTENIMIENTO: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/IMPRESORAS_NF")
def crear_impresora_nf(data: ImpresoraNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."IMPRESORAS_NF"
        ("ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
         "MARCA","MODELO","NUMERO_SERIE","TIPO","CANTIDAD",
         "IP","MAC","PROVEEDOR","COSTO","MONEDA",
         "UBICACION","ESTADO","PLANTA","DISPONIBLE",
         "FECHA_ASIGNACION","OBSERVACIONES",
         "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A",
         "PERSONAL_IT_ASIGNA","FECHA_MANTENIMIENTO")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_INVENTARIO, data.FECHA_ENTRADA, data.RECIBIDO_POR,
        data.MARCA, data.MODELO, data.NUMERO_SERIE, data.TIPO, data.CANTIDAD,
        data.IP, data.MAC, data.PROVEEDOR, data.COSTO, data.MONEDA,
        data.UBICACION, data.ESTADO, data.PLANTA, data.DISPONIBLE,
        data.FECHA_ASIGNACION, data.OBSERVACIONES,
        data.FECHA_SALIDA, data.DESTINO_PLANTA, data.ASIGNADO_A,
        data.PERSONAL_IT_ASIGNA, data.FECHA_MANTENIMIENTO
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/IMPRESORAS_NF")
def obtener_impresoras_nf(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    OC: str = None,
    FOLIO_INVENTARIO: str = None,
    FECHA_ENTRADA: str = None,
    RECIBIDO_POR: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NUMERO_SERIE: str = None,
    TIPO: str = None,
    CANTIDAD: str = None,
    IP: str = None,
    MAC: str = None,
    PROVEEDOR: str = None,
    COSTO: str = None,
    MONEDA: str = None,
    UBICACION: str = None,
    ESTADO: str = None,
    PLANTA: str = None,
    DISPONIBLE: str = None,
    FECHA_ASIGNACION: str = None,
    OBSERVACIONES: str = None,
    FECHA_SALIDA: str = None,
    DESTINO_PLANTA: str = None,
    ASIGNADO_A: str = None,
    PERSONAL_IT_ASIGNA: str = None,
    FECHA_MANTENIMIENTO: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO_INVENTARIO", FOLIO_INVENTARIO)
    af("FECHA_ENTRADA", FECHA_ENTRADA)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("TIPO", TIPO)
    af("CANTIDAD", CANTIDAD)
    af("IP", IP)
    af("MAC", MAC)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("UBICACION", UBICACION)
    af("ESTADO", ESTADO)
    af("PLANTA", PLANTA)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_ASIGNACION", FECHA_ASIGNACION)
    af("OBSERVACIONES", OBSERVACIONES)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)
    af("FECHA_MANTENIMIENTO", FECHA_MANTENIMIENTO)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."IMPRESORAS_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
            "MARCA","MODELO","NUMERO_SERIE","TIPO","CANTIDAD",
            "IP","MAC","PROVEEDOR","COSTO","MONEDA",
            "UBICACION","ESTADO","PLANTA","DISPONIBLE",
            "FECHA_ASIGNACION","OBSERVACIONES",
            "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A",
            "PERSONAL_IT_ASIGNA","FECHA_MANTENIMIENTO",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."IMPRESORAS_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/IMPRESORAS_NF/{id}")
def editar_impresora_nf(id: int, data: ImpresoraNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."IMPRESORAS_NF"
        SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO_INVENTARIO"=%s,"FECHA_ENTRADA"=%s,"RECIBIDO_POR"=%s,
        "MARCA"=%s,"MODELO"=%s,"NUMERO_SERIE"=%s,"TIPO"=%s,"CANTIDAD"=%s,
        "IP"=%s,"MAC"=%s,"PROVEEDOR"=%s,"COSTO"=%s,"MONEDA"=%s,
        "UBICACION"=%s,"ESTADO"=%s,"PLANTA"=%s,"DISPONIBLE"=%s,
        "FECHA_ASIGNACION"=%s,"OBSERVACIONES"=%s,
        "FECHA_SALIDA"=%s,"DESTINO_PLANTA"=%s,"ASIGNADO_A"=%s,
        "PERSONAL_IT_ASIGNA"=%s,"FECHA_MANTENIMIENTO"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_INVENTARIO, data.FECHA_ENTRADA, data.RECIBIDO_POR,
        data.MARCA, data.MODELO, data.NUMERO_SERIE, data.TIPO, data.CANTIDAD,
        data.IP, data.MAC, data.PROVEEDOR, data.COSTO, data.MONEDA,
        data.UBICACION, data.ESTADO, data.PLANTA, data.DISPONIBLE,
        data.FECHA_ASIGNACION, data.OBSERVACIONES,
        data.FECHA_SALIDA, data.DESTINO_PLANTA, data.ASIGNADO_A,
        data.PERSONAL_IT_ASIGNA, data.FECHA_MANTENIMIENTO, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/IMPRESORAS_NF/{id}")
def eliminar_impresora_nf(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."IMPRESORAS_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/IMPRESORAS_NF/PDF/{id}")
async def subir_pdf_impresora(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("IMPRESORAS_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."IMPRESORAS_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/IMPRESORAS_NF/PDF/{id}")
def ver_pdf_impresora(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."IMPRESORAS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/IMPRESORAS_NF/PDF/{id}")
def eliminar_pdf_impresora(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."IMPRESORAS_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."IMPRESORAS_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/IMPRESORAS_NF/EXPORTAR")
def exportar_impresoras(
    ID_UNICO: str = None, OC: str = None, FOLIO_INVENTARIO: str = None,
    FECHA_ENTRADA: str = None, RECIBIDO_POR: str = None, MARCA: str = None,
    MODELO: str = None, NUMERO_SERIE: str = None, TIPO: str = None,
    CANTIDAD: str = None, IP: str = None, MAC: str = None,
    PROVEEDOR: str = None, COSTO: str = None, MONEDA: str = None,
    UBICACION: str = None, ESTADO: str = None, PLANTA: str = None,
    DISPONIBLE: str = None, FECHA_ASIGNACION: str = None, OBSERVACIONES: str = None,
    FECHA_SALIDA: str = None, DESTINO_PLANTA: str = None, ASIGNADO_A: str = None,
    PERSONAL_IT_ASIGNA: str = None, FECHA_MANTENIMIENTO: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO_INVENTARIO", FOLIO_INVENTARIO)
    af("FECHA_ENTRADA", FECHA_ENTRADA)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("TIPO", TIPO)
    af("CANTIDAD", CANTIDAD)
    af("IP", IP)
    af("MAC", MAC)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("UBICACION", UBICACION)
    af("ESTADO", ESTADO)
    af("PLANTA", PLANTA)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_ASIGNACION", FECHA_ASIGNACION)
    af("OBSERVACIONES", OBSERVACIONES)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)
    af("FECHA_MANTENIMIENTO", FECHA_MANTENIMIENTO)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
               "MARCA","MODELO","NUMERO_SERIE","TIPO","CANTIDAD",
               "IP","MAC","PROVEEDOR","COSTO","MONEDA",
               "UBICACION","ESTADO","PLANTA","DISPONIBLE",
               "FECHA_ASIGNACION","OBSERVACIONES",
               "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A",
               "PERSONAL_IT_ASIGNA","FECHA_MANTENIMIENTO"
        FROM public."IMPRESORAS_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=impresoras_filtradas.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/IMPRESORAS_NF/EXPORTAR_TODO")
def exportar_todo_impresoras():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
               "MARCA","MODELO","NUMERO_SERIE","TIPO","CANTIDAD",
               "IP","MAC","PROVEEDOR","COSTO","MONEDA",
               "UBICACION","ESTADO","PLANTA","DISPONIBLE",
               "FECHA_ASIGNACION","OBSERVACIONES",
               "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A",
               "PERSONAL_IT_ASIGNA","FECHA_MANTENIMIENTO"
        FROM public."IMPRESORAS_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=impresoras_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/IMPRESORAS_NF/EXPORTAR_ANIO")
def exportar_impresoras_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_INVENTARIO","FECHA_ENTRADA","RECIBIDO_POR",
               "MARCA","MODELO","NUMERO_SERIE","TIPO","CANTIDAD",
               "IP","MAC","PROVEEDOR","COSTO","MONEDA",
               "UBICACION","ESTADO","PLANTA","DISPONIBLE",
               "FECHA_ASIGNACION","OBSERVACIONES",
               "FECHA_SALIDA","DESTINO_PLANTA","ASIGNADO_A",
               "PERSONAL_IT_ASIGNA","FECHA_MANTENIMIENTO"
        FROM public."IMPRESORAS_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_ENTRADA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=impresoras_{anio}.xlsx"}
    )

# ==========================================================
# =================== TELEFONIA_NF =========================
# ==========================================================


class TelefoniaNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NUMERO_SERIE: Optional[str] = None
    CANTIDAD: Optional[str] = None
    ACCESORIOS: Optional[str] = None
    MAC_WIFI: Optional[str] = None
    MAC_ETHERNET: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO: Optional[str] = None
    VIDA_UTIL_MESES: Optional[str] = None
    ESTADO: Optional[str] = None
    UBICACION: Optional[str] = None
    DISPONIBLE: Optional[str] = None
    FECHA_SALIDA: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    RESPONSABLE: Optional[str] = None
    PERSONAL_IT_ASIGNA: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/TELEFONIA_NF")
def crear_telefonia_nf(data: TelefoniaNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."TELEFONIA_NF"
        ("ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
         "SUBCATEGORIA","MARCA","MODELO","NUMERO_SERIE","CANTIDAD",
         "ACCESORIOS","MAC_WIFI","MAC_ETHERNET","PROVEEDOR","COSTO",
         "VIDA_UTIL_MESES","ESTADO","UBICACION","DISPONIBLE",
         "FECHA_SALIDA","DESTINO_PLANTA","RESPONSABLE","PERSONAL_IT_ASIGNA")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NUMERO_SERIE, data.CANTIDAD,
        data.ACCESORIOS, data.MAC_WIFI, data.MAC_ETHERNET, data.PROVEEDOR, data.COSTO,
        data.VIDA_UTIL_MESES, data.ESTADO, data.UBICACION, data.DISPONIBLE,
        data.FECHA_SALIDA, data.DESTINO_PLANTA, data.RESPONSABLE, data.PERSONAL_IT_ASIGNA
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/TELEFONIA_NF")
def obtener_telefonia_nf(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    OC: str = None,
    FOLIO: str = None,
    FECHA_REGISTRO: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NUMERO_SERIE: str = None,
    CANTIDAD: str = None,
    ACCESORIOS: str = None,
    MAC_WIFI: str = None,
    MAC_ETHERNET: str = None,
    PROVEEDOR: str = None,
    COSTO: str = None,
    VIDA_UTIL_MESES: str = None,
    ESTADO: str = None,
    UBICACION: str = None,
    DISPONIBLE: str = None,
    FECHA_SALIDA: str = None,
    DESTINO_PLANTA: str = None,
    RESPONSABLE: str = None,
    PERSONAL_IT_ASIGNA: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO", FOLIO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("ACCESORIOS", ACCESORIOS)
    af("MAC_WIFI", MAC_WIFI)
    af("MAC_ETHERNET", MAC_ETHERNET)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("VIDA_UTIL_MESES", VIDA_UTIL_MESES)
    af("ESTADO", ESTADO)
    af("UBICACION", UBICACION)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("RESPONSABLE", RESPONSABLE)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."TELEFONIA_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
            "SUBCATEGORIA","MARCA","MODELO","NUMERO_SERIE","CANTIDAD",
            "ACCESORIOS","MAC_WIFI","MAC_ETHERNET","PROVEEDOR","COSTO",
            "VIDA_UTIL_MESES","ESTADO","UBICACION","DISPONIBLE",
            "FECHA_SALIDA","DESTINO_PLANTA","RESPONSABLE","PERSONAL_IT_ASIGNA",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."TELEFONIA_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/TELEFONIA_NF/{id}")
def editar_telefonia_nf(id: int, data: TelefoniaNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."TELEFONIA_NF"
        SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO"=%s,"FECHA_REGISTRO"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,"NUMERO_SERIE"=%s,"CANTIDAD"=%s,
        "ACCESORIOS"=%s,"MAC_WIFI"=%s,"MAC_ETHERNET"=%s,"PROVEEDOR"=%s,"COSTO"=%s,
        "VIDA_UTIL_MESES"=%s,"ESTADO"=%s,"UBICACION"=%s,"DISPONIBLE"=%s,
        "FECHA_SALIDA"=%s,"DESTINO_PLANTA"=%s,"RESPONSABLE"=%s,"PERSONAL_IT_ASIGNA"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO, data.FECHA_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NUMERO_SERIE, data.CANTIDAD,
        data.ACCESORIOS, data.MAC_WIFI, data.MAC_ETHERNET, data.PROVEEDOR, data.COSTO,
        data.VIDA_UTIL_MESES, data.ESTADO, data.UBICACION, data.DISPONIBLE,
        data.FECHA_SALIDA, data.DESTINO_PLANTA, data.RESPONSABLE, data.PERSONAL_IT_ASIGNA, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/TELEFONIA_NF/{id}")
def eliminar_telefonia_nf(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."TELEFONIA_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/TELEFONIA_NF/PDF/{id}")
async def subir_pdf_telefonia(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("TELEFONIA_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."TELEFONIA_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/TELEFONIA_NF/PDF/{id}")
def ver_pdf_telefonia(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."TELEFONIA_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/TELEFONIA_NF/PDF/{id}")
def eliminar_pdf_telefonia(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."TELEFONIA_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."TELEFONIA_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/TELEFONIA_NF/EXPORTAR")
def exportar_telefonia(
    ID_UNICO: str = None, OC: str = None, FOLIO: str = None,
    FECHA_REGISTRO: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    MARCA: str = None, MODELO: str = None, NUMERO_SERIE: str = None,
    CANTIDAD: str = None, ACCESORIOS: str = None, MAC_WIFI: str = None,
    MAC_ETHERNET: str = None, PROVEEDOR: str = None, COSTO: str = None,
    VIDA_UTIL_MESES: str = None, ESTADO: str = None, UBICACION: str = None,
    DISPONIBLE: str = None, FECHA_SALIDA: str = None, DESTINO_PLANTA: str = None,
    RESPONSABLE: str = None, PERSONAL_IT_ASIGNA: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO", FOLIO)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NUMERO_SERIE", NUMERO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("ACCESORIOS", ACCESORIOS)
    af("MAC_WIFI", MAC_WIFI)
    af("MAC_ETHERNET", MAC_ETHERNET)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("VIDA_UTIL_MESES", VIDA_UTIL_MESES)
    af("ESTADO", ESTADO)
    af("UBICACION", UBICACION)
    af("DISPONIBLE", DISPONIBLE)
    af("FECHA_SALIDA", FECHA_SALIDA)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("RESPONSABLE", RESPONSABLE)
    af("PERSONAL_IT_ASIGNA", PERSONAL_IT_ASIGNA)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NUMERO_SERIE","CANTIDAD",
               "ACCESORIOS","MAC_WIFI","MAC_ETHERNET","PROVEEDOR","COSTO",
               "VIDA_UTIL_MESES","ESTADO","UBICACION","DISPONIBLE",
               "FECHA_SALIDA","DESTINO_PLANTA","RESPONSABLE","PERSONAL_IT_ASIGNA"
        FROM public."TELEFONIA_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=telefonia_filtrada.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/TELEFONIA_NF/EXPORTAR_TODO")
def exportar_todo_telefonia():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NUMERO_SERIE","CANTIDAD",
               "ACCESORIOS","MAC_WIFI","MAC_ETHERNET","PROVEEDOR","COSTO",
               "VIDA_UTIL_MESES","ESTADO","UBICACION","DISPONIBLE",
               "FECHA_SALIDA","DESTINO_PLANTA","RESPONSABLE","PERSONAL_IT_ASIGNA"
        FROM public."TELEFONIA_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=telefonia_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/TELEFONIA_NF/EXPORTAR_ANIO")
def exportar_telefonia_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NUMERO_SERIE","CANTIDAD",
               "ACCESORIOS","MAC_WIFI","MAC_ETHERNET","PROVEEDOR","COSTO",
               "VIDA_UTIL_MESES","ESTADO","UBICACION","DISPONIBLE",
               "FECHA_SALIDA","DESTINO_PLANTA","RESPONSABLE","PERSONAL_IT_ASIGNA"
        FROM public."TELEFONIA_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=telefonia_{anio}.xlsx"}
    )

# ==========================================================
# =================== CONSUMIBLES_NF =======================
# ==========================================================

class ConsumibleNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO_CANTIDAD: Optional[str] = None
    FECHA_ENTRADA: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    DESCRIPCION: Optional[str] = None
    CANTIDAD: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    PLANTA: Optional[str] = None
    UBICACION: Optional[str] = None
    DESTINO: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/CONSUMIBLES_NF")
def crear_consumible(data: ConsumibleNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."CONSUMIBLES_NF"
        ("ID_UNICO","OC","FOLIO_CANTIDAD","FECHA_ENTRADA","RECIBIDO_POR",
         "SUBCATEGORIA","MARCA","MODELO","DESCRIPCION","CANTIDAD",
         "PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACION","DESTINO")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_CANTIDAD, data.FECHA_ENTRADA,
        data.RECIBIDO_POR, data.SUBCATEGORIA, data.MARCA, data.MODELO,
        data.DESCRIPCION, data.CANTIDAD, data.PROVEEDOR, data.COSTO,
        data.MONEDA, data.PLANTA, data.UBICACION, data.DESTINO
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/CONSUMIBLES_NF")
def obtener_consumibles(
    page: int = Query(1, ge=1), limit: int = Query(10, ge=1),
    ID_UNICO: str = None, OC: str = None, FOLIO_CANTIDAD: str = None,
    FECHA_ENTRADA: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    MARCA: str = None, MODELO: str = None, DESCRIPCION: str = None,
    CANTIDAD: str = None, PROVEEDOR: str = None, COSTO: str = None,
    MONEDA: str = None, PLANTA: str = None, UBICACION: str = None,
    DESTINO: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()
    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO); af("OC", OC); af("FOLIO_CANTIDAD", FOLIO_CANTIDAD)
    af("FECHA_ENTRADA", FECHA_ENTRADA); af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA); af("MARCA", MARCA); af("MODELO", MODELO)
    af("DESCRIPCION", DESCRIPCION); af("CANTIDAD", CANTIDAD)
    af("PROVEEDOR", PROVEEDOR); af("COSTO", COSTO); af("MONEDA", MONEDA)
    af("PLANTA", PLANTA); af("UBICACION", UBICACION); af("DESTINO", DESTINO)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""
    cursor.execute(f'SELECT COUNT(*) FROM public."CONSUMIBLES_NF" {where_clause}', params)
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_CANTIDAD","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","DESCRIPCION","CANTIDAD",
               "PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACION","DESTINO",
               CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."CONSUMIBLES_NF" {where_clause}
        ORDER BY "ID" DESC LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]
    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/CONSUMIBLES_NF/{id}")
def editar_consumible(id: int, data: ConsumibleNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."CONSUMIBLES_NF" SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO_CANTIDAD"=%s,"FECHA_ENTRADA"=%s,
        "RECIBIDO_POR"=%s,"SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,
        "DESCRIPCION"=%s,"CANTIDAD"=%s,"PROVEEDOR"=%s,"COSTO"=%s,
        "MONEDA"=%s,"PLANTA"=%s,"UBICACION"=%s,"DESTINO"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO_CANTIDAD, data.FECHA_ENTRADA,
        data.RECIBIDO_POR, data.SUBCATEGORIA, data.MARCA, data.MODELO,
        data.DESCRIPCION, data.CANTIDAD, data.PROVEEDOR, data.COSTO,
        data.MONEDA, data.PLANTA, data.UBICACION, data.DESTINO, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/CONSUMIBLES_NF/{id}")
def eliminar_consumible(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."CONSUMIBLES_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/CONSUMIBLES_NF/PDF/{id}")
async def subir_pdf_consumible(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("CONSUMIBLES_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."CONSUMIBLES_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/CONSUMIBLES_NF/PDF/{id}")
def ver_pdf_consumible(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."CONSUMIBLES_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/CONSUMIBLES_NF/PDF/{id}")
def eliminar_pdf_consumible(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."CONSUMIBLES_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."CONSUMIBLES_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/CONSUMIBLES_NF/EXPORTAR")
def exportar_consumibles(
    ID_UNICO: str = None, OC: str = None, FOLIO_CANTIDAD: str = None,
    FECHA_ENTRADA: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    MARCA: str = None, MODELO: str = None, DESCRIPCION: str = None,
    CANTIDAD: str = None, PROVEEDOR: str = None, COSTO: str = None,
    MONEDA: str = None, PLANTA: str = None, UBICACION: str = None,
    DESTINO: str = None
):
    conn = get_connection()
    cursor = conn.cursor()
    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO); af("OC", OC); af("FOLIO_CANTIDAD", FOLIO_CANTIDAD)
    af("FECHA_ENTRADA", FECHA_ENTRADA); af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA); af("MARCA", MARCA); af("MODELO", MODELO)
    af("DESCRIPCION", DESCRIPCION); af("CANTIDAD", CANTIDAD)
    af("PROVEEDOR", PROVEEDOR); af("COSTO", COSTO); af("MONEDA", MONEDA)
    af("PLANTA", PLANTA); af("UBICACION", UBICACION); af("DESTINO", DESTINO)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""
    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_CANTIDAD","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","DESCRIPCION","CANTIDAD",
               "PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACION","DESTINO"
        FROM public."CONSUMIBLES_NF" {where} ORDER BY "ID" DESC
    """, params)
    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=consumibles_filtrados.xlsx"})

# ---------------- EXPORTAR TODO ----------------
@app.get("/CONSUMIBLES_NF/EXPORTAR_TODO")
def exportar_todo_consumibles():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_CANTIDAD","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","DESCRIPCION","CANTIDAD",
               "PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACION","DESTINO"
        FROM public."CONSUMIBLES_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=consumibles_todo.xlsx"})

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/CONSUMIBLES_NF/EXPORTAR_ANIO")
def exportar_consumibles_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO_CANTIDAD","FECHA_ENTRADA","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","DESCRIPCION","CANTIDAD",
               "PROVEEDOR","COSTO","MONEDA","PLANTA","UBICACION","DESTINO"
        FROM public."CONSUMIBLES_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_ENTRADA") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=consumibles_{anio}.xlsx"})

# ==========================================================
# ====================== RADIO_NF ==========================
# ==========================================================


class RadioNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO: Optional[str] = None
    FECHA_DE_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NO_SERIE: Optional[str] = None
    CANTIDAD: Optional[str] = None
    OBSERVACIONES: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    VIDA_UTIL: Optional[str] = None
    REQUISITOR: Optional[str] = None
    FECHA_DE_SALIDA: Optional[str] = None
    ASIGNADO_A: Optional[str] = None
    DESTINO_PLANTA: Optional[str] = None
    PERSONAL_IT_QUE_ASIGNA: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/RADIO_NF")
def crear_radio(data: RadioNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."RADIO_NF"
        ("ID_UNICO","OC","FOLIO","FECHA_DE_REGISTRO","RECIBIDO_POR",
         "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
         "OBSERVACIONES","PROVEEDOR","COSTO","MONEDA","VIDA_UTIL",
         "REQUISITOR","FECHA_DE_SALIDA","ASIGNADO_A",
         "DESTINO_PLANTA","PERSONAL_IT_QUE_ASIGNA")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,
                %s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.FOLIO, data.FECHA_DE_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE, data.CANTIDAD,
        data.OBSERVACIONES, data.PROVEEDOR, data.COSTO, data.MONEDA, data.VIDA_UTIL,
        data.REQUISITOR, data.FECHA_DE_SALIDA, data.ASIGNADO_A,
        data.DESTINO_PLANTA, data.PERSONAL_IT_QUE_ASIGNA
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/RADIO_NF")
def obtener_radios(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    OC: str = None,
    FOLIO: str = None,
    FECHA_DE_REGISTRO: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    MARCA: str = None,
    MODELO: str = None,
    NO_SERIE: str = None,
    CANTIDAD: str = None,
    OBSERVACIONES: str = None,
    PROVEEDOR: str = None,
    COSTO: str = None,
    MONEDA: str = None,
    VIDA_UTIL: str = None,
    REQUISITOR: str = None,
    FECHA_DE_SALIDA: str = None,
    ASIGNADO_A: str = None,
    DESTINO_PLANTA: str = None,
    PERSONAL_IT_QUE_ASIGNA: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO", FOLIO)
    af("FECHA_DE_REGISTRO", FECHA_DE_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NO_SERIE", NO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("OBSERVACIONES", OBSERVACIONES)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("VIDA_UTIL", VIDA_UTIL)
    af("REQUISITOR", REQUISITOR)
    af("FECHA_DE_SALIDA", FECHA_DE_SALIDA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("PERSONAL_IT_QUE_ASIGNA", PERSONAL_IT_QUE_ASIGNA)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."RADIO_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","OC","FOLIO","FECHA_DE_REGISTRO","RECIBIDO_POR",
            "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
            "OBSERVACIONES","PROVEEDOR","COSTO","MONEDA","VIDA_UTIL",
            "REQUISITOR","FECHA_DE_SALIDA","ASIGNADO_A",
            "DESTINO_PLANTA","PERSONAL_IT_QUE_ASIGNA",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."RADIO_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/RADIO_NF/{id}")
def editar_radio(id: str, data: RadioNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."RADIO_NF"
        SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO"=%s,"FECHA_DE_REGISTRO"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"MARCA"=%s,"MODELO"=%s,"NO_SERIE"=%s,"CANTIDAD"=%s,
        "OBSERVACIONES"=%s,"PROVEEDOR"=%s,"COSTO"=%s,"MONEDA"=%s,"VIDA_UTIL"=%s,
        "REQUISITOR"=%s,"FECHA_DE_SALIDA"=%s,"ASIGNADO_A"=%s,
        "DESTINO_PLANTA"=%s,"PERSONAL_IT_QUE_ASIGNA"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.FOLIO, data.FECHA_DE_REGISTRO, data.RECIBIDO_POR,
        data.SUBCATEGORIA, data.MARCA, data.MODELO, data.NO_SERIE, data.CANTIDAD,
        data.OBSERVACIONES, data.PROVEEDOR, data.COSTO, data.MONEDA, data.VIDA_UTIL,
        data.REQUISITOR, data.FECHA_DE_SALIDA, data.ASIGNADO_A,
        data.DESTINO_PLANTA, data.PERSONAL_IT_QUE_ASIGNA, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/RADIO_NF/{id}")
def eliminar_radio(id: str):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."RADIO_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/RADIO_NF/PDF/{id}")
async def subir_pdf_radio(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("RADIO_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."RADIO_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF GUARDADO"}

# ---------------- VER PDF ----------------
@app.get("/RADIO_NF/PDF/{id}")
def ver_pdf_radio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."RADIO_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/RADIO_NF/PDF/{id}")
def eliminar_pdf_radio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."RADIO_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."RADIO_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/RADIO_NF/EXPORTAR")
def exportar_radios(
    ID_UNICO: str = None, OC: str = None, FOLIO: str = None,
    FECHA_DE_REGISTRO: str = None, RECIBIDO_POR: str = None, SUBCATEGORIA: str = None,
    MARCA: str = None, MODELO: str = None, NO_SERIE: str = None,
    CANTIDAD: str = None, OBSERVACIONES: str = None, PROVEEDOR: str = None,
    COSTO: str = None, MONEDA: str = None, VIDA_UTIL: str = None,
    REQUISITOR: str = None, FECHA_DE_SALIDA: str = None, ASIGNADO_A: str = None,
    DESTINO_PLANTA: str = None, PERSONAL_IT_QUE_ASIGNA: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("FOLIO", FOLIO)
    af("FECHA_DE_REGISTRO", FECHA_DE_REGISTRO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("MARCA", MARCA)
    af("MODELO", MODELO)
    af("NO_SERIE", NO_SERIE)
    af("CANTIDAD", CANTIDAD)
    af("OBSERVACIONES", OBSERVACIONES)
    af("PROVEEDOR", PROVEEDOR)
    af("COSTO", COSTO)
    af("MONEDA", MONEDA)
    af("VIDA_UTIL", VIDA_UTIL)
    af("REQUISITOR", REQUISITOR)
    af("FECHA_DE_SALIDA", FECHA_DE_SALIDA)
    af("ASIGNADO_A", ASIGNADO_A)
    af("DESTINO_PLANTA", DESTINO_PLANTA)
    af("PERSONAL_IT_QUE_ASIGNA", PERSONAL_IT_QUE_ASIGNA)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_DE_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
               "OBSERVACIONES","PROVEEDOR","COSTO","MONEDA","VIDA_UTIL",
               "REQUISITOR","FECHA_DE_SALIDA","ASIGNADO_A",
               "DESTINO_PLANTA","PERSONAL_IT_QUE_ASIGNA"
        FROM public."RADIO_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=radio_filtrado.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/RADIO_NF/EXPORTAR_TODO")
def exportar_todo_radio():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_DE_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
               "OBSERVACIONES","PROVEEDOR","COSTO","MONEDA","VIDA_UTIL",
               "REQUISITOR","FECHA_DE_SALIDA","ASIGNADO_A",
               "DESTINO_PLANTA","PERSONAL_IT_QUE_ASIGNA"
        FROM public."RADIO_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=radio_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/RADIO_NF/EXPORTAR_ANIO")
def exportar_radio_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","FOLIO","FECHA_DE_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","MARCA","MODELO","NO_SERIE","CANTIDAD",
               "OBSERVACIONES","PROVEEDOR","COSTO","MONEDA","VIDA_UTIL",
               "REQUISITOR","FECHA_DE_SALIDA","ASIGNADO_A",
               "DESTINO_PLANTA","PERSONAL_IT_QUE_ASIGNA"
        FROM public."RADIO_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_DE_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=radio_{anio}.xlsx"}
    )

# ==========================================================
# ================ TINTAS_TONER_RIBON_NF ===================
# ==========================================================
# AGREGA ESTE BLOQUE EN backend.py

class TintaTonerRibonNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    MODELO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    STOCK: Optional[str] = None
    COSTO_MN: Optional[str] = None
    CANTIDAD_RECIBIDA: Optional[str] = None
    FECHA_INSTALACION: Optional[str] = None
    UBICACION: Optional[str] = None
    IMPRESORA: Optional[str] = None
    INSTALADO_POR: Optional[str] = None

# ---------------- CREAR ----------------
@app.post("/TINTAS_TONER_RIBON_NF")
def crear_tinta(data: TintaTonerRibonNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."TINTAS_TONER_RIBON_NF"
        ("ID_UNICO","OC","MODELO","RECIBIDO_POR","SUBCATEGORIA",
         "FECHA_REGISTRO","PROVEEDOR","STOCK","COSTO_MN",
         "CANTIDAD_RECIBIDA","FECHA_INSTALACION","UBICACION",
         "IMPRESORA","INSTALADO_POR")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (
        data.ID_UNICO, data.OC, data.MODELO, data.RECIBIDO_POR, data.SUBCATEGORIA,
        data.FECHA_REGISTRO, data.PROVEEDOR, data.STOCK, data.COSTO_MN,
        data.CANTIDAD_RECIBIDA, data.FECHA_INSTALACION, data.UBICACION,
        data.IMPRESORA, data.INSTALADO_POR
    ))
    new_id = cursor.fetchone()[0]
    conn.commit()
    cursor.close()
    conn.close()
    return {"ID": new_id}

# ---------------- PAGINACION CON FILTROS ----------------
@app.get("/TINTAS_TONER_RIBON_NF")
def obtener_tintas(
    page: int = Query(1, ge=1),
    limit: int = Query(10, ge=1),
    ID_UNICO: str = None,
    OC: str = None,
    MODELO: str = None,
    RECIBIDO_POR: str = None,
    SUBCATEGORIA: str = None,
    FECHA_REGISTRO: str = None,
    PROVEEDOR: str = None,
    STOCK: str = None,
    COSTO_MN: str = None,
    CANTIDAD_RECIBIDA: str = None,
    FECHA_INSTALACION: str = None,
    UBICACION: str = None,
    IMPRESORA: str = None,
    INSTALADO_POR: str = None
):
    offset = (page - 1) * limit
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("MODELO", MODELO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("PROVEEDOR", PROVEEDOR)
    af("STOCK", STOCK)
    af("COSTO_MN", COSTO_MN)
    af("CANTIDAD_RECIBIDA", CANTIDAD_RECIBIDA)
    af("FECHA_INSTALACION", FECHA_INSTALACION)
    af("UBICACION", UBICACION)
    af("IMPRESORA", IMPRESORA)
    af("INSTALADO_POR", INSTALADO_POR)

    where_clause = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(
        f'SELECT COUNT(*) FROM public."TINTAS_TONER_RIBON_NF" {where_clause}',
        params
    )
    total = cursor.fetchone()[0]

    cursor.execute(f"""
        SELECT
            "ID","ID_UNICO","OC","MODELO","RECIBIDO_POR","SUBCATEGORIA",
            "FECHA_REGISTRO","PROVEEDOR","STOCK","COSTO_MN",
            "CANTIDAD_RECIBIDA","FECHA_INSTALACION","UBICACION",
            "IMPRESORA","INSTALADO_POR",
            CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."TINTAS_TONER_RIBON_NF"
        {where_clause}
        ORDER BY "ID" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    rows = cursor.fetchall()
    columnas = [desc[0] for desc in cursor.description]
    resultado = [dict(zip(columnas, row)) for row in rows]

    cursor.close()
    conn.close()
    return {"total": total, "page": page, "limit": limit, "data": resultado}

# ---------------- EDITAR ----------------
@app.put("/TINTAS_TONER_RIBON_NF/{id}")
def editar_tinta(id: int, data: TintaTonerRibonNF):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."TINTAS_TONER_RIBON_NF"
        SET
        "ID_UNICO"=%s,"OC"=%s,"MODELO"=%s,"RECIBIDO_POR"=%s,"SUBCATEGORIA"=%s,
        "FECHA_REGISTRO"=%s,"PROVEEDOR"=%s,"STOCK"=%s,"COSTO_MN"=%s,
        "CANTIDAD_RECIBIDA"=%s,"FECHA_INSTALACION"=%s,"UBICACION"=%s,
        "IMPRESORA"=%s,"INSTALADO_POR"=%s
        WHERE "ID"=%s
    """, (
        data.ID_UNICO, data.OC, data.MODELO, data.RECIBIDO_POR, data.SUBCATEGORIA,
        data.FECHA_REGISTRO, data.PROVEEDOR, data.STOCK, data.COSTO_MN,
        data.CANTIDAD_RECIBIDA, data.FECHA_INSTALACION, data.UBICACION,
        data.IMPRESORA, data.INSTALADO_POR, id
    ))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ACTUALIZADO"}

# ---------------- ELIMINAR ----------------
@app.delete("/TINTAS_TONER_RIBON_NF/{id}")
def eliminar_tinta(id: int):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('DELETE FROM public."TINTAS_TONER_RIBON_NF" WHERE "ID"=%s', (id,))
    conn.commit()
    cursor.close()
    conn.close()
    return {"mensaje": "ELIMINADO"}

# ---------------- SUBIR PDF ----------------
@app.post("/TINTAS_TONER_RIBON_NF/PDF/{id}")
async def subir_pdf_tinta(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("TINTAS_TONER_RIBON_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."TINTAS_TONER_RIBON_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

# ---------------- VER PDF ----------------
@app.get("/TINTAS_TONER_RIBON_NF/PDF/{id}")
def ver_pdf_tinta(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."TINTAS_TONER_RIBON_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

# ---------------- ELIMINAR PDF ----------------
@app.delete("/TINTAS_TONER_RIBON_NF/PDF/{id}")
def eliminar_pdf_tinta(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."TINTAS_TONER_RIBON_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."TINTAS_TONER_RIBON_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

# ---------------- EXPORTAR FILTRADO ----------------
@app.get("/TINTAS_TONER_RIBON_NF/EXPORTAR")
def exportar_tintas(
    ID_UNICO: str = None, OC: str = None, MODELO: str = None,
    RECIBIDO_POR: str = None, SUBCATEGORIA: str = None, FECHA_REGISTRO: str = None,
    PROVEEDOR: str = None, STOCK: str = None, COSTO_MN: str = None,
    CANTIDAD_RECIBIDA: str = None, FECHA_INSTALACION: str = None,
    UBICACION: str = None, IMPRESORA: str = None, INSTALADO_POR: str = None
):
    conn = get_connection()
    cursor = conn.cursor()

    filtros = []
    params = []

    def af(campo, valor):
        if valor:
            filtros.append(f'"{campo}"::text ILIKE %s')
            params.append(f"%{valor}%")

    af("ID_UNICO", ID_UNICO)
    af("OC", OC)
    af("MODELO", MODELO)
    af("RECIBIDO_POR", RECIBIDO_POR)
    af("SUBCATEGORIA", SUBCATEGORIA)
    af("FECHA_REGISTRO", FECHA_REGISTRO)
    af("PROVEEDOR", PROVEEDOR)
    af("STOCK", STOCK)
    af("COSTO_MN", COSTO_MN)
    af("CANTIDAD_RECIBIDA", CANTIDAD_RECIBIDA)
    af("FECHA_INSTALACION", FECHA_INSTALACION)
    af("UBICACION", UBICACION)
    af("IMPRESORA", IMPRESORA)
    af("INSTALADO_POR", INSTALADO_POR)

    where = ("WHERE " + " AND ".join(filtros)) if filtros else ""

    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","MODELO","RECIBIDO_POR","SUBCATEGORIA",
               "FECHA_REGISTRO","PROVEEDOR","STOCK","COSTO_MN",
               "CANTIDAD_RECIBIDA","FECHA_INSTALACION","UBICACION",
               "IMPRESORA","INSTALADO_POR"
        FROM public."TINTAS_TONER_RIBON_NF" {where} ORDER BY "ID" DESC
    """, params)

    rows = cursor.fetchall()
    columns = [desc[0] for desc in cursor.description]
    df = pd.DataFrame(rows, columns=columns)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    cursor.close()
    conn.close()

    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=tintas_filtradas.xlsx"}
    )

# ---------------- EXPORTAR TODO ----------------
@app.get("/TINTAS_TONER_RIBON_NF/EXPORTAR_TODO")
def exportar_todo_tintas():
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","MODELO","RECIBIDO_POR","SUBCATEGORIA",
               "FECHA_REGISTRO","PROVEEDOR","STOCK","COSTO_MN",
               "CANTIDAD_RECIBIDA","FECHA_INSTALACION","UBICACION",
               "IMPRESORA","INSTALADO_POR"
        FROM public."TINTAS_TONER_RIBON_NF" ORDER BY "ID" DESC
    """, conn)
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=tintas_todo.xlsx"}
    )

# ---------------- EXPORTAR POR AÑO ----------------
@app.get("/TINTAS_TONER_RIBON_NF/EXPORTAR_ANIO")
def exportar_tintas_por_anio(anio: int):
    conn = get_connection()
    df = pd.read_sql("""
        SELECT "ID","ID_UNICO","OC","MODELO","RECIBIDO_POR","SUBCATEGORIA",
               "FECHA_REGISTRO","PROVEEDOR","STOCK","COSTO_MN",
               "CANTIDAD_RECIBIDA","FECHA_INSTALACION","UBICACION",
               "IMPRESORA","INSTALADO_POR"
        FROM public."TINTAS_TONER_RIBON_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO") = %s
        ORDER BY "ID" DESC
    """, conn, params=(anio,))
    output = io.BytesIO()
    df.to_excel(output, index=False)
    output.seek(0)
    conn.close()
    return StreamingResponse(
        output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f"attachment; filename=tintas_{anio}.xlsx"}
    )

# ==========================================================
# ============= SERVICIOS_PROVEEDORES_NF ===================
# ==========================================================

class ServicioNF(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO_COTIZACION: Optional[str] = None
    FOLIO_REPORTE: Optional[str] = None
    FECHA: Optional[str] = None
    REQUISITOR: Optional[str] = None
    CUENTA_POLIZA: Optional[str] = None
    SERVICIO_COSTO: Optional[str] = None
    UBICACION_PLANTA: Optional[str] = None
    AREA: Optional[str] = None
    CANTIDAD: Optional[str] = None
    DESC_SERVICIO: Optional[str] = None
    DESC_TRABAJO: Optional[str] = None
    MATERIAL_EQUIPO: Optional[str] = None
    OBSERVACIONES: Optional[str] = None
    PROVEEDORES: Optional[str] = None
    PANEL_FACEPLATE: Optional[str] = None
    SWITCH: Optional[str] = None
    PERSONAL_RECIBIO: Optional[str] = None
    SE_FINALIZO: Optional[str] = None
    COSTO: Optional[str] = None

@app.post("/SERVICIOS_PROVEEDORES_NF")
def crear_servicio(data: ServicioNF):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."SERVICIOS_PROVEEDORES_NF"
        ("ID_UNICO","OC","FOLIO_COTIZACION","FOLIO_REPORTE","FECHA","REQUISITOR",
         "CUENTA_POLIZA","SERVICIO_COSTO","UBICACION_PLANTA","AREA","CANTIDAD",
         "DESC_SERVICIO","DESC_TRABAJO","MATERIAL_EQUIPO","OBSERVACIONES",
         "PROVEEDORES","PANEL_FACEPLATE","SWITCH","PERSONAL_RECIBIO","SE_FINALIZO","COSTO")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (data.ID_UNICO,data.OC,data.FOLIO_COTIZACION,data.FOLIO_REPORTE,data.FECHA,
          data.REQUISITOR,data.CUENTA_POLIZA,data.SERVICIO_COSTO,data.UBICACION_PLANTA,
          data.AREA,data.CANTIDAD,data.DESC_SERVICIO,data.DESC_TRABAJO,data.MATERIAL_EQUIPO,
          data.OBSERVACIONES,data.PROVEEDORES,data.PANEL_FACEPLATE,data.SWITCH,
          data.PERSONAL_RECIBIO,data.SE_FINALIZO,data.COSTO))
    new_id = cursor.fetchone()[0]; conn.commit(); cursor.close(); conn.close()
    return {"ID": new_id}

@app.get("/SERVICIOS_PROVEEDORES_NF")
def obtener_servicios(
    page: int = Query(1,ge=1), limit: int = Query(10,ge=1),
    ID_UNICO: str=None, OC: str=None, FOLIO_COTIZACION: str=None,
    FOLIO_REPORTE: str=None, FECHA: str=None, REQUISITOR: str=None,
    CUENTA_POLIZA: str=None, SERVICIO_COSTO: str=None, UBICACION_PLANTA: str=None,
    AREA: str=None, CANTIDAD: str=None, DESC_SERVICIO: str=None,
    DESC_TRABAJO: str=None, PROVEEDORES: str=None, SE_FINALIZO: str=None, COSTO: str=None
):
    offset = (page-1)*limit
    conn = get_connection(); cursor = conn.cursor()
    filtros=[]; params=[]
    def af(c,v):
        if v: filtros.append(f'"{c}"::text ILIKE %s'); params.append(f"%{v}%")
    af("ID_UNICO",ID_UNICO); af("OC",OC); af("FOLIO_COTIZACION",FOLIO_COTIZACION)
    af("FOLIO_REPORTE",FOLIO_REPORTE); af("FECHA",FECHA); af("REQUISITOR",REQUISITOR)
    af("CUENTA_POLIZA",CUENTA_POLIZA); af("SERVICIO_COSTO",SERVICIO_COSTO)
    af("UBICACION_PLANTA",UBICACION_PLANTA); af("AREA",AREA); af("CANTIDAD",CANTIDAD)
    af("DESC_SERVICIO",DESC_SERVICIO); af("DESC_TRABAJO",DESC_TRABAJO)
    af("PROVEEDORES",PROVEEDORES); af("SE_FINALIZO",SE_FINALIZO); af("COSTO",COSTO)
    where = ("WHERE "+" AND ".join(filtros)) if filtros else ""
    cursor.execute(f'SELECT COUNT(*) FROM public."SERVICIOS_PROVEEDORES_NF" {where}',params)
    total = cursor.fetchone()[0]
    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_COTIZACION","FOLIO_REPORTE","FECHA","REQUISITOR",
               "CUENTA_POLIZA","SERVICIO_COSTO","UBICACION_PLANTA","AREA","CANTIDAD",
               "DESC_SERVICIO","DESC_TRABAJO","MATERIAL_EQUIPO","OBSERVACIONES",
               "PROVEEDORES","PANEL_FACEPLATE","SWITCH","PERSONAL_RECIBIO","SE_FINALIZO","COSTO",
               CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."SERVICIOS_PROVEEDORES_NF" {where}
        ORDER BY "ID" DESC LIMIT %s OFFSET %s
    """, params+[limit,offset])
    rows=cursor.fetchall(); cols=[d[0] for d in cursor.description]
    cursor.close(); conn.close()
    return {"total":total,"page":page,"limit":limit,"data":[dict(zip(cols,r)) for r in rows]}

@app.put("/SERVICIOS_PROVEEDORES_NF/{id}")
def editar_servicio(id: int, data: ServicioNF):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."SERVICIOS_PROVEEDORES_NF" SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO_COTIZACION"=%s,"FOLIO_REPORTE"=%s,"FECHA"=%s,
        "REQUISITOR"=%s,"CUENTA_POLIZA"=%s,"SERVICIO_COSTO"=%s,"UBICACION_PLANTA"=%s,
        "AREA"=%s,"CANTIDAD"=%s,"DESC_SERVICIO"=%s,"DESC_TRABAJO"=%s,"MATERIAL_EQUIPO"=%s,
        "OBSERVACIONES"=%s,"PROVEEDORES"=%s,"PANEL_FACEPLATE"=%s,"SWITCH"=%s,
        "PERSONAL_RECIBIO"=%s,"SE_FINALIZO"=%s,"COSTO"=%s WHERE "ID"=%s
    """, (data.ID_UNICO,data.OC,data.FOLIO_COTIZACION,data.FOLIO_REPORTE,data.FECHA,
          data.REQUISITOR,data.CUENTA_POLIZA,data.SERVICIO_COSTO,data.UBICACION_PLANTA,
          data.AREA,data.CANTIDAD,data.DESC_SERVICIO,data.DESC_TRABAJO,data.MATERIAL_EQUIPO,
          data.OBSERVACIONES,data.PROVEEDORES,data.PANEL_FACEPLATE,data.SWITCH,
          data.PERSONAL_RECIBIO,data.SE_FINALIZO,data.COSTO,id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje":"ACTUALIZADO"}

@app.delete("/SERVICIOS_PROVEEDORES_NF/{id}")
def eliminar_servicio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('DELETE FROM public."SERVICIOS_PROVEEDORES_NF" WHERE "ID"=%s',(id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje":"ELIMINADO"}

@app.post("/SERVICIOS_PROVEEDORES_NF/PDF/{id}")
async def subir_pdf_servicio(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("SERVICIOS_PROVEEDORES_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."SERVICIOS_PROVEEDORES_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

@app.get("/SERVICIOS_PROVEEDORES_NF/PDF/{id}")
def ver_pdf_servicio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."SERVICIOS_PROVEEDORES_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

@app.delete("/SERVICIOS_PROVEEDORES_NF/PDF/{id}")
def eliminar_pdf_servicio(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."SERVICIOS_PROVEEDORES_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."SERVICIOS_PROVEEDORES_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

@app.get("/SERVICIOS_PROVEEDORES_NF/EXPORTAR_TODO")
def exportar_todo_servicios():
    conn = get_connection()
    df = pd.read_sql('SELECT "ID","ID_UNICO","OC","FOLIO_COTIZACION","FOLIO_REPORTE","FECHA","REQUISITOR","CUENTA_POLIZA","SERVICIO_COSTO","UBICACION_PLANTA","AREA","CANTIDAD","DESC_SERVICIO","DESC_TRABAJO","MATERIAL_EQUIPO","OBSERVACIONES","PROVEEDORES","PANEL_FACEPLATE","SWITCH","PERSONAL_RECIBIO","SE_FINALIZO","COSTO" FROM public."SERVICIOS_PROVEEDORES_NF" ORDER BY "ID" DESC',conn)
    output=io.BytesIO(); df.to_excel(output,index=False); output.seek(0); conn.close()
    return StreamingResponse(output,media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",headers={"Content-Disposition":"attachment; filename=servicios_todo.xlsx"})

# ==========================================================
# ============= DIRECTORIO_PROVEEDORES_NF ==================
# ==========================================================

class DirectorioNF(BaseModel):
    COMPANIA: Optional[str] = None
    RFC: Optional[str] = None
    CONTACTO: Optional[str] = None
    CEL: Optional[str] = None
    CORREO: Optional[str] = None
    NOTAS: Optional[str] = None

@app.post("/DIRECTORIO_PROVEEDORES_NF")
def crear_directorio(data: DirectorioNF):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."DIRECTORIO_PROVEEDORES_NF"
        ("COMPANIA","RFC","CONTACTO","CEL","CORREO","NOTAS")
        VALUES (%s,%s,%s,%s,%s,%s) RETURNING "ID";
    """, (data.COMPANIA,data.RFC,data.CONTACTO,data.CEL,data.CORREO,data.NOTAS))
    new_id=cursor.fetchone()[0]; conn.commit(); cursor.close(); conn.close()
    return {"ID":new_id}

@app.get("/DIRECTORIO_PROVEEDORES_NF")
def obtener_directorio(
    page: int=Query(1,ge=1), limit: int=Query(10,ge=1),
    COMPANIA: str=None, RFC: str=None, CONTACTO: str=None,
    CEL: str=None, CORREO: str=None, NOTAS: str=None
):
    offset=(page-1)*limit
    conn=get_connection(); cursor=conn.cursor()
    filtros=[]; params=[]
    def af(c,v):
        if v: filtros.append(f'"{c}"::text ILIKE %s'); params.append(f"%{v}%")
    af("COMPANIA",COMPANIA); af("RFC",RFC); af("CONTACTO",CONTACTO)
    af("CEL",CEL); af("CORREO",CORREO); af("NOTAS",NOTAS)
    where=("WHERE "+" AND ".join(filtros)) if filtros else ""
    cursor.execute(f'SELECT COUNT(*) FROM public."DIRECTORIO_PROVEEDORES_NF" {where}',params)
    total=cursor.fetchone()[0]
    cursor.execute(f'SELECT "ID","COMPANIA","RFC","CONTACTO","CEL","CORREO","NOTAS" FROM public."DIRECTORIO_PROVEEDORES_NF" {where} ORDER BY "ID" DESC LIMIT %s OFFSET %s',params+[limit,offset])
    rows=cursor.fetchall(); cols=[d[0] for d in cursor.description]
    cursor.close(); conn.close()
    return {"total":total,"page":page,"limit":limit,"data":[dict(zip(cols,r)) for r in rows]}

@app.put("/DIRECTORIO_PROVEEDORES_NF/{id}")
def editar_directorio(id: int, data: DirectorioNF):
    conn=get_connection(); cursor=conn.cursor()
    cursor.execute('UPDATE public."DIRECTORIO_PROVEEDORES_NF" SET "COMPANIA"=%s,"RFC"=%s,"CONTACTO"=%s,"CEL"=%s,"CORREO"=%s,"NOTAS"=%s WHERE "ID"=%s',
        (data.COMPANIA,data.RFC,data.CONTACTO,data.CEL,data.CORREO,data.NOTAS,id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje":"ACTUALIZADO"}

@app.delete("/DIRECTORIO_PROVEEDORES_NF/{id}")
def eliminar_directorio(id: int):
    conn=get_connection(); cursor=conn.cursor()
    cursor.execute('DELETE FROM public."DIRECTORIO_PROVEEDORES_NF" WHERE "ID"=%s',(id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje":"ELIMINADO"}

@app.get("/DIRECTORIO_PROVEEDORES_NF/EXPORTAR_TODO")
def exportar_todo_directorio():
    conn=get_connection()
    df=pd.read_sql('SELECT "ID","COMPANIA","RFC","CONTACTO","CEL","CORREO","NOTAS" FROM public."DIRECTORIO_PROVEEDORES_NF" ORDER BY "ID" DESC',conn)
    output=io.BytesIO(); df.to_excel(output,index=False); output.seek(0); conn.close()
    return StreamingResponse(output,media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",headers={"Content-Disposition":"attachment; filename=directorio_todo.xlsx"})


# ==========================================================
# ================ EQUIPO_RED_NF ======================
# ==========================================================

class RefaccionRed(BaseModel):
    ID_UNICO: Optional[str] = None
    OC: Optional[str] = None
    FOLIO_CORRECTIVO: Optional[str] = None
    FECHA_REGISTRO: Optional[str] = None
    RECIBIDO_POR: Optional[str] = None
    SUBCATEGORIA: Optional[str] = None
    NO_PARTE: Optional[str] = None
    MARCA: Optional[str] = None
    MODELO: Optional[str] = None
    NUMERO_SERIE: Optional[str] = None
    MAC1: Optional[str] = None
    MAC2: Optional[str] = None
    MAC_ADDRESS: Optional[str] = None
    CANTIDAD: Optional[str] = None
    PROVEEDOR: Optional[str] = None
    COSTO: Optional[str] = None
    MONEDA: Optional[str] = None
    UBICACION: Optional[str] = None
    DESTINO: Optional[str] = None
    OBSERVACIONES: Optional[str] = None
    ACTIVO_DTR3: Optional[str] = None

@app.post("/REFACCIONES_RED_NF")
def crear_refaccion_red(data: RefaccionRed):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO public."REFACCIONES_RED_NF"
        ("ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR","SUBCATEGORIA",
         "NO_PARTE","MARCA","MODELO","NUMERO_SERIE","MAC1","MAC2","MAC_ADDRESS",
         "CANTIDAD","PROVEEDOR","COSTO","MONEDA","UBICACION","DESTINO",
         "OBSERVACIONES","ACTIVO_DTR3")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
        RETURNING "ID";
    """, (data.ID_UNICO,data.OC,data.FOLIO_CORRECTIVO,data.FECHA_REGISTRO,data.RECIBIDO_POR,
          data.SUBCATEGORIA,data.NO_PARTE,data.MARCA,data.MODELO,data.NUMERO_SERIE,
          data.MAC1,data.MAC2,data.MAC_ADDRESS,data.CANTIDAD,data.PROVEEDOR,
          data.COSTO,data.MONEDA,data.UBICACION,data.DESTINO,data.OBSERVACIONES,data.ACTIVO_DTR3))
    new_id=cursor.fetchone()[0]; conn.commit(); cursor.close(); conn.close()
    return {"ID": new_id}

@app.get("/REFACCIONES_RED_NF")
def obtener_refacciones_red(
    page: int=Query(1,ge=1), limit: int=Query(10,ge=1),
    ID_UNICO: str=None, OC: str=None, FOLIO_CORRECTIVO: str=None,
    FECHA_REGISTRO: str=None, RECIBIDO_POR: str=None, SUBCATEGORIA: str=None,
    NO_PARTE: str=None, MARCA: str=None, MODELO: str=None, NUMERO_SERIE: str=None,
    MAC1: str=None, MAC2: str=None, MAC_ADDRESS: str=None, CANTIDAD: str=None,
    PROVEEDOR: str=None, COSTO: str=None, MONEDA: str=None, UBICACION: str=None,
    DESTINO: str=None, ACTIVO_DTR3: str=None
):
    offset=(page-1)*limit
    conn=get_connection(); cursor=conn.cursor()
    filtros=[]; params=[]
    def af(c,v):
        if v: filtros.append(f'"{c}"::text ILIKE %s'); params.append(f"%{v}%")
    af("ID_UNICO",ID_UNICO); af("OC",OC); af("FOLIO_CORRECTIVO",FOLIO_CORRECTIVO)
    af("FECHA_REGISTRO",FECHA_REGISTRO); af("RECIBIDO_POR",RECIBIDO_POR)
    af("SUBCATEGORIA",SUBCATEGORIA); af("NO_PARTE",NO_PARTE); af("MARCA",MARCA)
    af("MODELO",MODELO); af("NUMERO_SERIE",NUMERO_SERIE); af("MAC1",MAC1)
    af("MAC2",MAC2); af("MAC_ADDRESS",MAC_ADDRESS); af("CANTIDAD",CANTIDAD)
    af("PROVEEDOR",PROVEEDOR); af("COSTO",COSTO); af("MONEDA",MONEDA)
    af("UBICACION",UBICACION); af("DESTINO",DESTINO); af("ACTIVO_DTR3",ACTIVO_DTR3)
    where=("WHERE "+" AND ".join(filtros)) if filtros else ""
    cursor.execute(f'SELECT COUNT(*) FROM public."REFACCIONES_RED_NF" {where}',params)
    total=cursor.fetchone()[0]
    cursor.execute(f"""
        SELECT "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO","RECIBIDO_POR",
               "SUBCATEGORIA","NO_PARTE","MARCA","MODELO","NUMERO_SERIE",
               "MAC1","MAC2","MAC_ADDRESS","CANTIDAD","PROVEEDOR","COSTO","MONEDA",
               "UBICACION","DESTINO","OBSERVACIONES","ACTIVO_DTR3",
               CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."REFACCIONES_RED_NF" {where}
        ORDER BY "ID" DESC LIMIT %s OFFSET %s
    """, params+[limit,offset])
    rows=cursor.fetchall(); cols=[d[0] for d in cursor.description]
    cursor.close(); conn.close()
    return {"total":total,"page":page,"limit":limit,"data":[dict(zip(cols,r)) for r in rows]}

@app.put("/REFACCIONES_RED_NF/{id}")
def editar_refaccion_red(id: int, data: RefaccionRed):
    conn=get_connection(); cursor=conn.cursor()
    cursor.execute("""
        UPDATE public."REFACCIONES_RED_NF" SET
        "ID_UNICO"=%s,"OC"=%s,"FOLIO_CORRECTIVO"=%s,"FECHA_REGISTRO"=%s,"RECIBIDO_POR"=%s,
        "SUBCATEGORIA"=%s,"NO_PARTE"=%s,"MARCA"=%s,"MODELO"=%s,"NUMERO_SERIE"=%s,
        "MAC1"=%s,"MAC2"=%s,"MAC_ADDRESS"=%s,"CANTIDAD"=%s,"PROVEEDOR"=%s,
        "COSTO"=%s,"MONEDA"=%s,"UBICACION"=%s,"DESTINO"=%s,
        "OBSERVACIONES"=%s,"ACTIVO_DTR3"=%s WHERE "ID"=%s
    """, (data.ID_UNICO,data.OC,data.FOLIO_CORRECTIVO,data.FECHA_REGISTRO,data.RECIBIDO_POR,
          data.SUBCATEGORIA,data.NO_PARTE,data.MARCA,data.MODELO,data.NUMERO_SERIE,
          data.MAC1,data.MAC2,data.MAC_ADDRESS,data.CANTIDAD,data.PROVEEDOR,
          data.COSTO,data.MONEDA,data.UBICACION,data.DESTINO,
          data.OBSERVACIONES,data.ACTIVO_DTR3,id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje":"ACTUALIZADO"}

@app.delete("/REFACCIONES_RED_NF/{id}")
def eliminar_refaccion_red(id: int):
    conn=get_connection(); cursor=conn.cursor()
    cursor.execute('DELETE FROM public."REFACCIONES_RED_NF" WHERE "ID"=%s',(id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje":"ELIMINADO"}

@app.post("/REFACCIONES_RED_NF/PDF/{id}")
async def subir_pdf_refaccion_red(id: int, file: UploadFile = File(...)):
    contenido = await file.read()
    ruta = get_pdf_path("REFACCIONES_RED_NF", id)
    ruta.write_bytes(contenido)
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('UPDATE public."REFACCIONES_RED_NF" SET "PDF"=%s WHERE "ID"=%s', (str(ruta), id))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF guardado"}

@app.get("/REFACCIONES_RED_NF/PDF/{id}")
def ver_pdf_refaccion_red(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."REFACCIONES_RED_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone(); cursor.close(); conn.close()
    if not row or not row[0]:
        return {"error": "No existe PDF"}
    ruta = Path(row[0])
    if not ruta.exists():
        return {"error": "Archivo no encontrado en disco"}
    with open(ruta, "rb") as f:
        data = f.read()
    return Response(content=data, media_type="application/pdf")

@app.delete("/REFACCIONES_RED_NF/PDF/{id}")
def eliminar_pdf_refaccion_red(id: int):
    conn = get_connection(); cursor = conn.cursor()
    cursor.execute('SELECT "PDF" FROM public."REFACCIONES_RED_NF" WHERE "ID"=%s', (id,))
    row = cursor.fetchone()
    if row and row[0]:
        ruta = Path(row[0])
        if ruta.exists(): ruta.unlink()
    cursor.execute('UPDATE public."REFACCIONES_RED_NF" SET "PDF"=NULL WHERE "ID"=%s', (id,))
    conn.commit(); cursor.close(); conn.close()
    return {"mensaje": "PDF eliminado"}

@app.get("/REFACCIONES_RED_NF/EXPORTAR_TODO")
def exportar_todo_refacciones_red():
    conn=get_connection()
    df=pd.read_sql("""SELECT "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO",
        "RECIBIDO_POR","SUBCATEGORIA","NO_PARTE","MARCA","MODELO","NUMERO_SERIE",
        "MAC1","MAC2","MAC_ADDRESS","CANTIDAD","PROVEEDOR","COSTO","MONEDA",
        "UBICACION","DESTINO","OBSERVACIONES","ACTIVO_DTR3"
        FROM public."REFACCIONES_RED_NF" ORDER BY "ID" DESC""",conn)
    output=io.BytesIO(); df.to_excel(output,index=False); output.seek(0); conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition":"attachment; filename=refacciones_red_todo.xlsx"})

@app.get("/REFACCIONES_RED_NF/EXPORTAR_ANIO")
def exportar_refacciones_red_anio(anio: int):
    conn=get_connection()
    df=pd.read_sql("""SELECT "ID","ID_UNICO","OC","FOLIO_CORRECTIVO","FECHA_REGISTRO",
        "RECIBIDO_POR","SUBCATEGORIA","NO_PARTE","MARCA","MODELO","NUMERO_SERIE",
        "MAC1","MAC2","MAC_ADDRESS","CANTIDAD","PROVEEDOR","COSTO","MONEDA",
        "UBICACION","DESTINO","OBSERVACIONES","ACTIVO_DTR3"
        FROM public."REFACCIONES_RED_NF"
        WHERE EXTRACT(YEAR FROM "FECHA_REGISTRO")=%s ORDER BY "ID" DESC""",conn,params=(anio,))
    output=io.BytesIO(); df.to_excel(output,index=False); output.seek(0); conn.close()
    return StreamingResponse(output,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition":f"attachment; filename=refacciones_red_{anio}.xlsx"})




# ==========================================================
# ================ BUSCAR GLOBAL ===========================
# ==========================================================

@app.get("/BUSCAR_GLOBAL")
def buscar_global(q: str = Query(..., min_length=1)):
    """
    Busca el término 'q' en TODOS los módulos con OR entre columnas.
    Devuelve lista de módulos con resultados.
    """
    conn = get_connection()
    cursor = conn.cursor()

    MODULOS = [
        ("PREVENTIVOS", "MANTENIMIENTOS_PREVENTIVOS", ['ID_EQUIPO','UBICACION','PLAZO','REALIZADO_POR','FECHA_REALIZACION','OBSERVACIONES','nombre_dispositivo','PLANTA','CATEGORIA_COLOR']),
        ("CORRECTIVOS", "MANTENIMIENTOS_CORRECTIVOS", ['STATUS','FOLIO','PLANTA','LINEA_PERSONA','EQUIPO','MARCA','MODELO','NUMERO_SERIE','DESCRIPCION_FALLA','ACCESORIO_SOLICITADO','FECHA_SOLICITUD','REPORTE_ELABORADO_POR','TIPO_OBSERVACION','TIPO_CORRECTIVO','VENCIMIENTO_DIAS','FECHA_CONTEO_ACTUAL','FECHA_LIMITE_CIERRE','CATEGORIA_CORRECTIVO','REFACCION_ACCESORIO_COMPRA','FECHA_LLEGADA_REFACCION','FECHA_REPARACION','QUIEN_REALIZO_REPARACION','VALIDACION_FUNCIONAMIENTO','DESCRIPCION_REPARACION','OBSERVACIONES','OC_FACTURA']),
        ("BAJAS", "BAJAS_EQUIPOS", ['FOLIO','ESTADO','PLANTA','FECHA','EQUIPO','MARCA','MODELO','NO_SERIE','ACTIVO_FIJO','UBICACION_PERSONA','MOTIVO_BAJA','DIAGNOSTICO','COMENTARIOS','MOTIVO_CANCELACION']),
        ("REQ vs OC", "PRESUPUESTOS_REQ_VS_OC", ['NO_REQUISICION','ORDEN_COMPRA','FECHA_COMPRA','PO_SUBTOTAL','MONEDA','OC_SUBTOTAL','REGISTRADA_EN_OC']),
        ("ORDENES DE COMPRA", "ORDENES_DE_COMPRA", ['ORDEN_DE_COMPRA','FOLIO_CORRECTIVO_SOLICITUD_INVENTARIO','SOLICITANTE','PRESUPUESTO_MES','SERIE_UBICACION_NO_EMPLEADO','ACCESORIO_SOLICITADO','PROVEEDOR_ELEGIDO','PIEZA_SERVICIO','CANTIDAD','PRECIO_UNITARIO','TOTAL_NO_INCLUYE_IVA','MONEDA','COMENTARIOS','REQUISICION','FECHA_OC','OC','FECHA_ENTRADA','CANTIDAD_REGISTRADA','ESTATUS_OC','HOJA_CONTROL']),
        ("PANTALLAS NF", "PANTALLAS_NF", ['ID_UNICO','SUBCATEGORIA','MARCA','MODELO','NO_SERIE','CANTIDAD','TAMANO_PULGADAS','ACCESORIOS','MAC_WIFI','MAC_ETHERNET','PROVEEDOR','COSTO_USD','VIDA_UTIL_MESES','ESTADO','DISPONIBLE','FECHA_SALIDA','DESTINO_PLANTA','ASIGNADO_A','PERSONAL_IT_ASIGNA']),
        ("REFACCIONES NF", "REFACCIONES_NF", ['OC','FOLIO_CORRECTIVO','FECHA_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','MARCA','MODELO','SERIE','CANTIDAD','NUM_PARTE','COSTO','MONEDA','PROVEEDOR','DISPONIBLE','COMENTARIOS']),
        ("ACCESORIOS NF", "ACCESORIOS_NF", ['OC','FOLIO','FECHA_ENTRADA','RECIBIDO_POR','SUBCATEGORIA','MARCA','MODELO','NO_SERIE','CANTIDAD','TIPO','ACCESORIOS','PROVEEDOR','COSTO','MONEDA','DISPONIBLE','FECHA_SALIDA','ASIGNADO_A','DESTINO_PLANTA','PERSONAL_IT_ASIGNA']),
        ("DISPOSITIVOS NF", "DISPOSITIVOS_NF", ['ID_UNICO','OC','FOLIO','FECHA_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','MARCA','MODELO','NO_SERIE','CANTIDAD','COSTO','PROVEEDOR','ACTIVO_FIJO','PROCESADOR','ARQUITECTURA','ALMACENAMIENTO','TIPO_DISCO','SISTEMA_OPERATIVO','LICENCIA_SO','MEMORIA_RAM','VELOCIDAD_MEMORIA','TIPO_MEMORIA','SLOT_MEMORIA','MAX_MEMORIA','MODELO_CARGADOR','NO_SERIE_ELIMINADOR','BATERIA_LAPTOP','WIFI_MAC','ETH_MAC','ACCESORIOS','UBICACION','EDIFICIO','FECHA_SALIDA','ASIGNADO_A','DESTINO_PLANTA','DISPONIBLE','PERSONAL_IT_ASIGNA','FORMATO_BAJA']),
        ("INVENTARIOS", "INVENTARIOS", ['INV_FOLIO','EQUIPO','MARCA','MODELO','CANTIDAD','PRECIO_UNITARIO','MONEDA','PROVEEDOR','PRESUPUESTO','STATUS','ANO','OC','NUMERO_SERIE','UBICACION_ACTUAL']),
        ("CAMARAS DE AUDIO", "CAMARAS_DE_AUDIO", ['OC','FOLIO_INVENTARIO','FECHA_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','TIPO','MARCA','MODELO','NUMERO_SERIE','PROVEEDOR','CANTIDAD','COSTO','MONEDA','DESTINO','ACCESORIOS','FECHA_SALIDA','PLANTA','DESTINO2','PERSONAL_IT_ASIGNA','FOLIO_SERVICIO']),
        ("PERIFERICOS NF", "PERIFERICOS_NF", ['ID_UNICO','OC','FOLIO_INVENTARIO','FECHA_ENTRADA','RECIBIDO_POR','SUBCATEGORIA','TIPO','MARCA','MODELO','NUMERO_DE_SERIE','PROVEEDOR','COSTO_PESOS','ESTADO','DESTINO','DISPONIBLE','FECHA_SALIDA','DESTINO_PLANTA','ASIGNADO_A','PERSONAL_IT_ASIGNA']),
        ("HERRAMIENTA NF", "HERRAMIENTA_NF", ['ID_UNICO','OC','FOLIO_CORRECTIVO','FECHA_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','TIPO_USO','MARCA','MODELO','CANTIDAD','NUMERO_SERIE','NUM_PARTE','COSTO','MONEDA','PROVEEDOR','UBICACION','COMENTARIOS']),
        ("IMPRESORAS NF", "IMPRESORAS_NF", ['ID_UNICO','OC','FOLIO_INVENTARIO','FECHA_ENTRADA','RECIBIDO_POR','MARCA','MODELO','NUMERO_SERIE','TIPO','CANTIDAD','IP','MAC','PROVEEDOR','COSTO','MONEDA','UBICACION','ESTADO','PLANTA','DISPONIBLE','FECHA_ASIGNACION','OBSERVACIONES','FECHA_SALIDA','DESTINO_PLANTA','ASIGNADO_A','PERSONAL_IT_ASIGNA','FECHA_MANTENIMIENTO']),
        ("TELEFONIA NF", "TELEFONIA_NF", ['ID_UNICO','OC','FOLIO','FECHA_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','MARCA','MODELO','NUMERO_SERIE','CANTIDAD','ACCESORIOS','MAC_WIFI','MAC_ETHERNET','PROVEEDOR','COSTO','VIDA_UTIL_MESES','ESTADO','UBICACION','DISPONIBLE','FECHA_SALIDA','DESTINO_PLANTA','RESPONSABLE','PERSONAL_IT_ASIGNA']),
        ("CONSUMIBLES NF", "CONSUMIBLES_NF", ['ID_UNICO','OC','FOLIO_CANTIDAD','FECHA_ENTRADA','RECIBIDO_POR','SUBCATEGORIA','MARCA','MODELO','DESCRIPCION','CANTIDAD','PROVEEDOR','COSTO','MONEDA','PLANTA','UBICACION','DESTINO']),
        ("RADIOS NF", "RADIO_NF", ['ID_UNICO','OC','FOLIO','FECHA_DE_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','MARCA','MODELO','NO_SERIE','CANTIDAD','OBSERVACIONES','PROVEEDOR','COSTO','MONEDA','VIDA_UTIL','REQUISITOR','FECHA_DE_SALIDA','ASIGNADO_A','DESTINO_PLANTA','PERSONAL_IT_QUE_ASIGNA']),
        ("TINTAS TONER RIBON", "TINTAS_TONER_RIBON_NF", ['ID_UNICO','OC','MODELO','RECIBIDO_POR','SUBCATEGORIA','FECHA_REGISTRO','PROVEEDOR','STOCK','COSTO_MN','CANTIDAD_RECIBIDA','FECHA_INSTALACION','UBICACION','IMPRESORA','INSTALADO_POR']),
        ("SERVICIOS PROVEEDORES", "SERVICIOS_PROVEEDORES_NF", ['ID_UNICO','OC','FOLIO_COTIZACION','FOLIO_REPORTE','FECHA','REQUISITOR','CUENTA_POLIZA','SERVICIO_COSTO','UBICACION_PLANTA','AREA','CANTIDAD','DESC_SERVICIO','DESC_TRABAJO','MATERIAL_EQUIPO','OBSERVACIONES','PROVEEDORES','PANEL_FACEPLATE','SWITCH','PERSONAL_RECIBIO','SE_FINALIZO','COSTO']),
        ("DIRECTORIO PROVEEDORES", "DIRECTORIO_PROVEEDORES_NF", ['COMPANIA','RFC','CONTACTO','CEL','CORREO','NOTAS']),
        ("EQUIPO DE RED NF", "REFACCIONES_RED_NF", ['ID_UNICO','OC','FOLIO_CORRECTIVO','FECHA_REGISTRO','RECIBIDO_POR','SUBCATEGORIA','NO_PARTE','MARCA','MODELO','NUMERO_SERIE','MAC1','MAC2','MAC_ADDRESS','CANTIDAD','PROVEEDOR','COSTO','MONEDA','UBICACION','DESTINO','OBSERVACIONES','ACTIVO_DTR3']),
    ]

    resultados = []
    termino = f"%{q}%"

    for nombre, tabla, columnas in MODULOS:
        try:
            # Construir WHERE con OR en todas las columnas
            condiciones = " OR ".join([f'"{c}"::text ILIKE %s' for c in columnas])
            params = [termino] * len(columnas)

            # Contar total
            cursor.execute(f'SELECT COUNT(*) FROM public."{tabla}" WHERE {condiciones}', params)
            total = cursor.fetchone()[0]

            if total == 0:
                continue

            # Traer hasta 50 registros
            cursor.execute(f'SELECT * FROM public."{tabla}" WHERE {condiciones} LIMIT 50', params)
            rows = cursor.fetchall()
            col_names = [desc[0] for desc in cursor.description]
            # Excluir columnas binarias
            col_names_clean = [c for c in col_names if c not in ('PDF',)]
            data = []
            for row in rows:
                row_dict = dict(zip(col_names, row))
                data.append({k: (str(v) if v is not None else "") for k, v in row_dict.items() if k in col_names_clean})

            resultados.append({
                "modulo": nombre,
                "tabla": tabla,
                "total": total,
                "registros": data
            })
        except Exception as e:
            continue

    cursor.close()
    conn.close()

    return {
        "termino": q,
        "modulos_con_resultados": len(resultados),
        "resultados": resultados
    }

# ==========================================================
# =================== SISTEMA DE USUARIOS ==================
# ==========================================================
import hashlib
from datetime import datetime

def hash_password(password: str) -> str:
    return hashlib.sha256(password.encode()).hexdigest()

# ── SQL PARA CREAR LA TABLA (ejecutar una sola vez en pgAdmin) ──
# CREATE TABLE IF NOT EXISTS public."USUARIOS" (
#     "ID"               SERIAL PRIMARY KEY,
#     "USUARIO"          character varying NOT NULL UNIQUE,
#     "NOMBRE"           character varying NOT NULL,
#     "PASSWORD_HASH"    character varying NOT NULL,
#     "ROL"              character varying NOT NULL,  -- ADMIN | USUARIO
#     "PASSWORD_TEMPORAL" boolean DEFAULT true,
#     "ACTIVO"           boolean DEFAULT true,
#     "CREATED_AT"       timestamp DEFAULT CURRENT_TIMESTAMP,
#     "ULTIMO_ACCESO"    timestamp
# );
#
# -- INSERT usuarios iniciales (contraseña temporal: mexico123)
# INSERT INTO public."USUARIOS" ("USUARIO","NOMBRE","PASSWORD_HASH","ROL","PASSWORD_TEMPORAL") VALUES
# ('RODRIGUEZFL','FLOR RODRIGUEZ','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','ADMIN',true),
# ('LUNAI','LUNA I','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true),
# ('CHAVEZS','CHAVEZ S','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true),
# ('SIFUENTESR','SIFUENTES R','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true),
# ('GARCIALU','GARCIA LU','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true),
# ('PENAV','PEÑA V','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true),
# ('GANDARAJA','GANDARA JA','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true),
# ('VLOPEZ','V LOPEZ','b977536b80f275309305ef2a9197bc912f957577a1c2235216c7a197e3d6525d','USUARIO',true);

class LoginRequest(BaseModel):
    usuario: str
    password: str

class CambioPasswordRequest(BaseModel):
    usuario: str
    password_actual: str
    password_nuevo: str

# ---------------- LOGIN ----------------
@app.post("/LOGIN")
def login(data: LoginRequest):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        SELECT "ID","USUARIO","NOMBRE","ROL","PASSWORD_HASH","PASSWORD_TEMPORAL","ACTIVO"
        FROM public."USUARIOS"
        WHERE "USUARIO" = %s
    """, (data.usuario.upper(),))
    row = cursor.fetchone()

    if not row:
        cursor.close(); conn.close()
        return {"ok": False, "mensaje": "Usuario o contraseña incorrectos"}

    id_, usuario, nombre, rol, pwd_hash, pwd_temporal, activo = row

    if not activo:
        cursor.close(); conn.close()
        return {"ok": False, "mensaje": "Usuario desactivado"}

    if hash_password(data.password) != pwd_hash:
        cursor.close(); conn.close()
        return {"ok": False, "mensaje": "Usuario o contraseña incorrectos"}

    # Actualizar ultimo acceso
    cursor.execute('UPDATE public."USUARIOS" SET "ULTIMO_ACCESO"=%s WHERE "ID"=%s',
                   (datetime.now(), id_))
    conn.commit()
    cursor.close(); conn.close()

    return {
        "ok": True,
        "usuario": usuario,
        "nombre": nombre,
        "rol": rol,
        "password_temporal": pwd_temporal
    }

# ---------------- CAMBIO DE CONTRASEÑA ----------------
@app.post("/CAMBIAR_PASSWORD")
def cambiar_password(data: CambioPasswordRequest):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        SELECT "ID","PASSWORD_HASH"
        FROM public."USUARIOS"
        WHERE "USUARIO" = %s
    """, (data.usuario.upper(),))
    row = cursor.fetchone()

    if not row:
        cursor.close(); conn.close()
        return {"ok": False, "mensaje": "Usuario no encontrado"}

    id_, pwd_hash = row

    if hash_password(data.password_actual) != pwd_hash:
        cursor.close(); conn.close()
        return {"ok": False, "mensaje": "Contraseña actual incorrecta"}

    if len(data.password_nuevo) < 6:
        cursor.close(); conn.close()
        return {"ok": False, "mensaje": "La nueva contraseña debe tener al menos 6 caracteres"}

    nuevo_hash = hash_password(data.password_nuevo)
    cursor.execute("""
        UPDATE public."USUARIOS"
        SET "PASSWORD_HASH"=%s, "PASSWORD_TEMPORAL"=false
        WHERE "ID"=%s
    """, (nuevo_hash, id_))
    conn.commit()
    cursor.close(); conn.close()

    return {"ok": True, "mensaje": "Contraseña actualizada correctamente"}

# ---------------- OBTENER PERMISOS ----------------
@app.get("/PERMISOS/{usuario}")
def obtener_permisos(usuario: str):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        SELECT "ROL","ACTIVO" FROM public."USUARIOS"
        WHERE "USUARIO" = %s
    """, (usuario.upper(),))
    row = cursor.fetchone()
    cursor.close(); conn.close()

    if not row:
        return {"ok": False}

    rol, activo = row
    return {
        "ok": True,
        "rol": rol,
        "puede_eliminar": rol == "ADMIN",
        "puede_editar": True,
        "puede_crear": True,
        "activo": activo
    }

# ---------------- ADMIN: LISTAR USUARIOS ----------------
@app.get("/ADMIN/USUARIOS")
def listar_usuarios():
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        SELECT "ID","USUARIO","NOMBRE","ROL","PASSWORD_TEMPORAL","ACTIVO","CREATED_AT","ULTIMO_ACCESO"
        FROM public."USUARIOS" ORDER BY "ID"
    """)
    rows = cursor.fetchall()
    cols = [d[0] for d in cursor.description]
    cursor.close(); conn.close()
    return [dict(zip(cols, r)) for r in rows]

# ---------------- ADMIN: RESETEAR CONTRASEÑA ----------------
@app.post("/ADMIN/RESET_PASSWORD/{usuario}")
def reset_password(usuario: str):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        UPDATE public."USUARIOS"
        SET "PASSWORD_HASH"=%s, "PASSWORD_TEMPORAL"=true
        WHERE "USUARIO"=%s
    """, (hash_password("mexico123"), usuario.upper()))
    conn.commit()
    cursor.close(); conn.close()
    return {"ok": True, "mensaje": f"Contraseña de {usuario} reseteada a mexico123"}

# ---------------- ADMIN: ACTIVAR/DESACTIVAR USUARIO ----------------
@app.put("/ADMIN/USUARIOS/{usuario}/ACTIVO")
def toggle_usuario(usuario: str, activo: bool):
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute('UPDATE public."USUARIOS" SET "ACTIVO"=%s WHERE "USUARIO"=%s',
                   (activo, usuario.upper()))
    conn.commit()
    cursor.close(); conn.close()
    return {"ok": True}