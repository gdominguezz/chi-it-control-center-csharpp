from fastapi import APIRouter, UploadFile, File, Query, Request
from fastapi.responses import StreamingResponse, Response
from pydantic import BaseModel
from typing import Optional
from datetime import datetime
from pathlib import Path
from fastapi.responses import HTMLResponse
from dateutil.relativedelta import relativedelta
import hashlib, json, io
import pandas as pd
import qrcode
from database import get_connection
from urllib.parse import quote
from PIL import Image, ImageDraw, ImageFont
from openpyxl.styles import PatternFill
from fastapi import Request

# ───────── CARPETA QR ─────────
QR_DIR = Path("QR_CODES/MESAS")
QR_DIR.mkdir(parents=True, exist_ok=True)


router = APIRouter()

# ── Constantes ───────────────────────────────────────────
PDF_DIR = Path("PDF_DATABASE/PREVENTIVOS")
PDF_DIR.mkdir(parents=True, exist_ok=True)


# ── Modelo ───────────────────────────────────────────────
class Preventivo(BaseModel):
    ID_EQUIPO:         Optional[str] = None
    UBICACION:         Optional[str] = None
    PLAZO:             Optional[str] = None
    REALIZADO_POR:     Optional[str] = None
    FECHA_REALIZACION: Optional[str] = None
    OBSERVACIONES:     Optional[str] = None
    nombre_dispositivo:Optional[str] = None
    PLANTA:            Optional[str] = None
    CATEGORIA_COLOR:   Optional[str] = None
    OBSERVACIONES:     Optional[str] = None

# ── Tabla de auditoría (se crea al importar el router) ───
def _crear_tabla_auditoria():
    conn = get_connection(); cursor = conn.cursor()
    try:
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS public."AUDITORIA_PREVENTIVOS" (
                "ID"                SERIAL PRIMARY KEY,
                "REGISTRO_ID"       INTEGER NOT NULL,
                "FECHA_CAMBIO"      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                "USUARIO"           VARCHAR(100),
                "REGISTRO_ANTERIOR" TEXT,
                "REGISTRO_NUEVO"    TEXT
            )
        """)
        conn.commit()
    except Exception as e:
        print(f"Error creando AUDITORIA_PREVENTIVOS: {e}")
    finally:
        cursor.close(); conn.close()

_crear_tabla_auditoria()


# ── Helpers ──────────────────────────────────────────────
def _registrar_auditoria(registro_id, usuario, anterior, nuevo):
    try:
        conn = get_connection(); cursor = conn.cursor()
        cursor.execute("""
            INSERT INTO public."AUDITORIA_PREVENTIVOS"
            ("REGISTRO_ID","USUARIO","REGISTRO_ANTERIOR","REGISTRO_NUEVO","FECHA_CAMBIO")
            VALUES (%s,%s,%s,%s,%s)
        """, (registro_id, usuario,
              json.dumps(anterior, default=str),
              json.dumps(nuevo,    default=str),
              datetime.now()))
        conn.commit(); cursor.close(); conn.close()
    except Exception as e:
        print(f"Error auditoria preventivo: {e}")


def _obtener_usuario(request: Request, usuario_query: str = None) -> str:
    """Obtiene el usuario desde cookie, header X-Usuario o query param."""
    try:
        usr = request.cookies.get("usuario")
        if usr: return usr
        usr = request.headers.get("X-Usuario")
        if usr: return usr
    except Exception:
        pass
    return usuario_query or "SISTEMA"


def _pdf_path(id: int) -> Path:
    return PDF_DIR / f"{id}.pdf"



@router.get("/PREVENTIVOS")
def obtener_preventivos(
    page: int = Query(1),
    limit: int = Query(10),
    ID_EQUIPO: Optional[str] = Query(None),
    UBICACION: Optional[str] = Query(None),
    nombre_dispositivo: Optional[str] = Query(None),
    PLANTA: Optional[str] = Query(None),
    CATEGORIA_COLOR: Optional[str] = Query(None)
):

    conn = get_connection()
    cursor = conn.cursor()

    where = "WHERE 1=1"
    params = []

    if ID_EQUIPO:
        where += ' AND "ID_EQUIPO" ILIKE %s'
        params.append(f"%{ID_EQUIPO}%")

    if UBICACION:
        where += ' AND "UBICACION" ILIKE %s'
        params.append(f"%{UBICACION}%")

    if nombre_dispositivo:
        where += ' AND "nombre_dispositivo" ILIKE %s'
        params.append(f"%{nombre_dispositivo}%")

    if PLANTA:
        where += ' AND "PLANTA" ILIKE %s'
        params.append(f"%{PLANTA}%")

    if CATEGORIA_COLOR:
        where += ' AND "CATEGORIA_COLOR" ILIKE %s'
        params.append(f"%{CATEGORIA_COLOR}%")

    # total de registros
    cursor.execute(
        f'SELECT COUNT(*) FROM public."MANTENIMIENTOS_PREVENTIVOS" {where}',
        params
    )

    total = cursor.fetchone()[0]

    offset = (page - 1) * limit

    cursor.execute(f"""
        SELECT *,
        CASE WHEN "PDF" IS NOT NULL THEN true ELSE false END AS "TIENE_PDF"
        FROM public."MANTENIMIENTOS_PREVENTIVOS"
        {where}
        ORDER BY "Id" DESC
        LIMIT %s OFFSET %s
    """, params + [limit, offset])

    columnas = [d[0] for d in cursor.description]
    data = [dict(zip(columnas, row)) for row in cursor.fetchall()]

    cursor.close()
    conn.close()

    return {
        "data": data,
        "total": total,
        "page": page
    }
# ════════════════════════════════════════════════════════
# JALAR LOS DATOS DEL PREVENTIVO 
# ════════════════════════════════════════════════════════
@router.get("/PREVENTIVO/DATOS/{id}")
def obtener_datos_preventivo(id: int, usuario: str = None):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    SELECT
        "UBICACION",
        "PLANTA"
    FROM public."MANTENIMIENTOS_PREVENTIVOS"
    WHERE "Id"=%s
    """,(id,))

    base = cursor.fetchone()

    if not base:
        cursor.close()
        conn.close()
        return {"error":"Registro no encontrado"}

    ubicacion = base[0]
    planta = base[1]

    cursor.execute("""
    SELECT
        "ID_EQUIPO",
        "nombre_dispositivo"
    FROM public."MANTENIMIENTOS_PREVENTIVOS"
    WHERE "UBICACION"=%s
    """,(ubicacion,))

    rows = cursor.fetchall()

    pc=""
    impresora=""
    ups=""

    for r in rows:

        equipo = r[0]
        dispositivo = (r[1] or "").upper()

        if "COMPUTADORA" in dispositivo or "CPU" in dispositivo or "PORTATIL" in dispositivo:
            pc = equipo

        if "IMPRESORA" in dispositivo:
            impresora = equipo

        if "UPS" in dispositivo:
            ups = equipo

    nombre_usuario = ""

    if usuario:

        cursor.execute("""
        SELECT "NOMBRE"
        FROM public."USUARIOS"
        WHERE "USUARIO"=%s
        """,(usuario.upper(),))

        user_row = cursor.fetchone()

        if user_row:
            nombre_usuario = user_row[0]

    cursor.close()
    conn.close()

    return {
        "planta": planta,
        "ubicacion": ubicacion,
        "usuario": nombre_usuario,
        "pc": pc,
        "impresora": impresora,
        "ups": ups
    }
# ════════════════════════════════════════════════════════
# GET  /PREVENTIVOS/{id}/HISTORIAL
# ════════════════════════════════════════════════════════
@router.get("/PREVENTIVOS/{id}/HISTORIAL")
def obtener_historial_preventivo(id: int):
    try:
        conn = get_connection(); cursor = conn.cursor()

        # Registro actual
        cursor.execute("""
            SELECT "Id","ID_EQUIPO","UBICACION","PLAZO","REALIZADO_POR",
                   "FECHA_REALIZACION","OBSERVACIONES","nombre_dispositivo","PLANTA","CATEGORIA_COLOR"
            FROM public."MANTENIMIENTOS_PREVENTIVOS" WHERE "Id"=%s
        """, (id,))
        row = cursor.fetchone()
        if not row:
            cursor.close(); conn.close()
            return {"success": False, "error": "Registro no encontrado"}

        cols_actual  = [d[0] for d in cursor.description]
        reg_actual   = dict(zip(cols_actual, row))

        # Historial
        cursor.execute("""
            SELECT "ID","FECHA_CAMBIO","USUARIO","REGISTRO_ANTERIOR","REGISTRO_NUEVO"
            FROM public."AUDITORIA_PREVENTIVOS"
            WHERE "REGISTRO_ID"=%s ORDER BY "FECHA_CAMBIO" DESC
        """, (id,))

        historial = []
        for r in cursor.fetchall():
            historial.append({
                "id":                r[0],
                "fecha":             r[1].isoformat() if r[1] else None,
                "usuario":           r[2],
                "registro_anterior": json.loads(r[3]) if r[3] else {},
                "registro_nuevo":    json.loads(r[4]) if r[4] else {}
            })

        cursor.close(); conn.close()
        return {"success": True, "registro_actual": reg_actual, "historial": historial}
    except Exception as e:
        return {"success": False, "error": str(e)}


# ════════════════════════════════════════════════════════
# POST /PREVENTIVO
# ════════════════════════════════════════════════════════
@router.post("/PREVENTIVO")
def crear_preventivo(data: Preventivo):
    conn = get_connection(); cursor = conn.cursor()
    try:
        cursor.execute("""
            INSERT INTO public."MANTENIMIENTOS_PREVENTIVOS"
            ("ID_EQUIPO","UBICACION","PLAZO","REALIZADO_POR",
             "FECHA_REALIZACION","OBSERVACIONES","nombre_dispositivo","PLANTA","CATEGORIA_COLOR")
            VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s) RETURNING "Id"
        """, (data.ID_EQUIPO, data.UBICACION, data.PLAZO, data.REALIZADO_POR,
              data.FECHA_REALIZACION, data.OBSERVACIONES,
              data.nombre_dispositivo, data.PLANTA, data.CATEGORIA_COLOR))
        new_id = cursor.fetchone()[0]
        conn.commit(); cursor.close(); conn.close()
        return {"ID": new_id}
    except Exception as e:
        cursor.close(); conn.close()
        return {"error": str(e)}
    
@router.get("/QR_GENERAR_TODOS")
def generar_qr_todos():

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    SELECT DISTINCT "UBICACION"
    FROM public."MANTENIMIENTOS_PREVENTIVOS"
    WHERE "UBICACION" IS NOT NULL
    """)

    ubicaciones = cursor.fetchall()

    cursor.close()
    conn.close()

    generados = []

    for u in ubicaciones:

        ubicacion = u[0]

        try:
            generar_qr_mesa(ubicacion)
            generados.append(ubicacion)

        except Exception as e:
            print("Error con", ubicacion, e)

    return {
        "mensaje": "QR generados",
        "total": len(generados),
        "ubicaciones": generados
    }


# ════════════════════════════════════════════════════════
# PUT  /PREVENTIVO/{id}
# ════════════════════════════════════════════════════════
@router.put("/PREVENTIVO/{id}")
def editar_preventivo(
    id: int, data: Preventivo, request: Request,
    usuario: Optional[str] = Query(None)
):
    conn = get_connection(); cursor = conn.cursor()
    try:
        # Registro anterior
        cursor.execute("""
            SELECT "ID_EQUIPO","UBICACION","PLAZO","REALIZADO_POR",
                   "FECHA_REALIZACION","OBSERVACIONES","nombre_dispositivo","PLANTA","CATEGORIA_COLOR"
            FROM public."MANTENIMIENTOS_PREVENTIVOS" WHERE "Id"=%s
        """, (id,))
        row = cursor.fetchone()
        anterior = {
            "ID_EQUIPO": row[0], "UBICACION": row[1], "PLAZO": row[2],
            "REALIZADO_POR": row[3],
            "FECHA_REALIZACION": str(row[4]) if row[4] else None,
            "OBSERVACIONES": row[5], "nombre_dispositivo": row[6],
            "PLANTA": row[7], "CATEGORIA_COLOR": row[8]
        } if row else {}

        # Actualizar
        cursor.execute("""
            UPDATE public."MANTENIMIENTOS_PREVENTIVOS" SET
            "ID_EQUIPO"=%s,"UBICACION"=%s,"PLAZO"=%s,"REALIZADO_POR"=%s,
            "FECHA_REALIZACION"=%s,"OBSERVACIONES"=%s,
            "nombre_dispositivo"=%s,"PLANTA"=%s,"CATEGORIA_COLOR"=%s
            WHERE "Id"=%s
        """, (data.ID_EQUIPO, data.UBICACION, data.PLAZO, data.REALIZADO_POR,
              data.FECHA_REALIZACION, data.OBSERVACIONES,
              data.nombre_dispositivo, data.PLANTA, data.CATEGORIA_COLOR, id))
        conn.commit()

        # Auditoría
        nuevo = {
            "ID_EQUIPO": data.ID_EQUIPO, "UBICACION": data.UBICACION,
            "PLAZO": data.PLAZO, "REALIZADO_POR": data.REALIZADO_POR,
            "FECHA_REALIZACION": data.FECHA_REALIZACION,
            "OBSERVACIONES": data.OBSERVACIONES,
            "nombre_dispositivo": data.nombre_dispositivo,
            "PLANTA": data.PLANTA, "CATEGORIA_COLOR": data.CATEGORIA_COLOR
        }
        usr = _obtener_usuario(request, usuario)
        _registrar_auditoria(id, usr, anterior, nuevo)

        cursor.close(); conn.close()
        return {"mensaje": "ACTUALIZADO"}
    except Exception as e:
        cursor.close(); conn.close()
        return {"error": str(e)}


# ════════════════════════════════════════════════════════
# DELETE /PREVENTIVO/{id}
# ════════════════════════════════════════════════════════
@router.delete("/PREVENTIVO/{id}")
def eliminar_preventivo(id: int, request: Request):

    usuario = request.cookies.get("usuario")

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    SELECT "ROL"
    FROM public."USUARIOS"
    WHERE "USUARIO"=%s
    """,(usuario,))

    rol = cursor.fetchone()[0]

    if rol != "ADMIN":
        return {"error":"No tienes permiso para eliminar"}

    cursor.execute("""
    DELETE FROM public."MANTENIMIENTOS_PREVENTIVOS"
    WHERE "Id"=%s
    """,(id,))

    conn.commit()

    cursor.close()
    conn.close()

    return {"ok":True}


# ════════════════════════════════════════════════════════
# PDF
# ════════════════════════════════════════════════════════
@router.post("/PREVENTIVO/PDF/{id}")
async def subir_pdf_preventivo(id: int, file: UploadFile = File(...)):
    try:
        contenido = await file.read()
        ruta = _pdf_path(id)
        ruta.write_bytes(contenido)

        conn = get_connection(); cursor = conn.cursor()
        cursor.execute('UPDATE public."MANTENIMIENTOS_PREVENTIVOS" SET "PDF"=%s WHERE "Id"=%s',
                       (str(ruta), id))
        conn.commit(); cursor.close(); conn.close()
        return {"mensaje": "PDF subido"}
    except Exception as e:
        return {"error": str(e)}


@router.get("/PREVENTIVO/PDF/{id}")
def obtener_pdf_preventivo(id: int):
    ruta = _pdf_path(id)
    if not ruta.exists():
        return {"error": "PDF no encontrado"}
    return Response(content=ruta.read_bytes(), media_type="application/pdf")


@router.delete("/PREVENTIVO/PDF/{id}")
def eliminar_pdf_preventivo(id: int):
    try:
        ruta = _pdf_path(id)
        if ruta.exists(): ruta.unlink()
        conn = get_connection(); cursor = conn.cursor()
        cursor.execute('UPDATE public."MANTENIMIENTOS_PREVENTIVOS" SET "PDF"=NULL WHERE "Id"=%s', (id,))
        conn.commit(); cursor.close(); conn.close()
        return {"mensaje": "PDF eliminado"}
    except Exception as e:
        return {"error": str(e)}


# ════════════════════════════════════════════════════════
# EXPORTAR
# ════════════════════════════════════════════════════════
@router.get("/PREVENTIVOS/EXPORTAR_TODO")
def exportar_preventivos_todo():

    conn = get_connection()

    df = pd.read_sql(
        'SELECT * FROM public."MANTENIMIENTOS_PREVENTIVOS" ORDER BY "Id" DESC',
        conn
    )

    conn.close()

    buffer = io.BytesIO()

    with pd.ExcelWriter(buffer, engine="openpyxl") as writer:

        df.to_excel(writer, index=False, sheet_name="Preventivos")

        ws = writer.book.active

        for row in range(2, len(df)+2):

            color = str(df.iloc[row-2]["CATEGORIA_COLOR"]).lower()

            fill = None

            if "verde" in color:
                fill = PatternFill(start_color="BBF7D0", end_color="BBF7D0", fill_type="solid")

            elif "amarillo" in color:
                fill = PatternFill(start_color="FEF9C3", end_color="FEF9C3", fill_type="solid")

            elif "rojo" in color:
                fill = PatternFill(start_color="FECACA", end_color="FECACA", fill_type="solid")

            elif "gris" in color:
                fill = PatternFill(start_color="E5E7EB", end_color="E5E7EB", fill_type="solid")

            elif "rosa" in color:
                fill = PatternFill(start_color="FFC5D3", end_color="FFC5D3", fill_type="solid")

            elif "azul" in color:
                fill = PatternFill(start_color="BEDFFB", end_color="BEDFFB", fill_type="solid")

            if fill:
                for col in range(1, len(df.columns)+1):
                    ws.cell(row=row, column=col).fill = fill

    buffer.seek(0)

    return StreamingResponse(
        buffer,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": "attachment; filename=preventivos_todo.xlsx"}
    )


##################################EXPORTAR FILTRADO###################

@router.get("/PREVENTIVOS/EXPORTAR_FILTRADO")
def exportar_preventivos_filtrado(
    ID_EQUIPO: Optional[str] = Query(None),
    UBICACION: Optional[str] = Query(None),
    nombre_dispositivo: Optional[str] = Query(None),
    PLANTA: Optional[str] = Query(None),
    CATEGORIA_COLOR: Optional[str] = Query(None),
    OBSERVACIONES: Optional[str] = Query(None)
):

    conn = get_connection()

    where = "WHERE 1=1"
    params = []

    if ID_EQUIPO:
        where += ' AND "ID_EQUIPO" ILIKE %s'
        params.append(f"%{ID_EQUIPO}%")

    if UBICACION:
        where += ' AND "UBICACION" ILIKE %s'
        params.append(f"%{UBICACION}%")

    if nombre_dispositivo:
        where += ' AND "nombre_dispositivo" ILIKE %s'
        params.append(f"%{nombre_dispositivo}%")

    if PLANTA:
        where += ' AND "PLANTA" ILIKE %s'
        params.append(f"%{PLANTA}%")

    if CATEGORIA_COLOR:
        where += ' AND "CATEGORIA_COLOR" ILIKE %s'
        params.append(f"%{CATEGORIA_COLOR}%")

    if OBSERVACIONES:
        where += ' AND "OBSERVACIONES" ILIKE %s'
        params.append(f"%{OBSERVACIONES}%")

    query = f"""
    SELECT
    "Id",
    "ID_EQUIPO",
    "UBICACION",
    "nombre_dispositivo",
    "PLANTA",
    "CATEGORIA_COLOR",
    "OBSERVACIONES"
    FROM public."MANTENIMIENTOS_PREVENTIVOS"
    {where}
    ORDER BY "Id" DESC
    """

    df = pd.read_sql(query, conn, params=params)
    conn.close()

    # ───────── COLORES ─────────
    def aplicar_color(row):

        color = str(row["CATEGORIA_COLOR"]).lower()

        if "verde" in color:
            return ['background-color: #bbf7d0']*len(row)

        if "amarillo" in color:
            return ['background-color: #fef9c3']*len(row)

        if "rojo" in color:
            return ['background-color: #fecaca']*len(row)

        if "gris" in color:
            return ['background-color: #e5e7eb']*len(row)

        if "rosa" in color:
            return ['background-color: #ffc5d3']*len(row)

        if "azul" in color:
            return ['background-color: #bedffb']*len(row)

        return ['']*len(row)

    styled = df.style.apply(aplicar_color, axis=1)

    buffer = io.BytesIO()

    with pd.ExcelWriter(buffer, engine="openpyxl") as writer:
        styled.to_excel(writer, index=False, sheet_name="Preventivos")

    buffer.seek(0)

    return StreamingResponse(
        buffer,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={
            "Content-Disposition": "attachment; filename=preventivos_filtrados.xlsx"
        }
    )

@router.get("/PREVENTIVOS/EXPORTAR_ANIO")
def exportar_preventivos_anio(anio: int = Query(...)):

    conn = get_connection()

    df = pd.read_sql("""
        SELECT *
        FROM public."MANTENIMIENTOS_PREVENTIVOS"
        WHERE EXTRACT(YEAR FROM "FECHA_REALIZACION") = %s
        ORDER BY "Id" DESC
    """, conn, params=(anio,))

    conn.close()

    buffer = io.BytesIO()

    with pd.ExcelWriter(buffer, engine="openpyxl") as writer:

        df.to_excel(writer, index=False, sheet_name="Preventivos")

        ws = writer.book.active

        for row in range(2, len(df)+2):

            color = str(df.iloc[row-2]["CATEGORIA_COLOR"]).lower()

            fill = None

            if "verde" in color:
                fill = PatternFill(start_color="BBF7D0", end_color="BBF7D0", fill_type="solid")

            elif "amarillo" in color:
                fill = PatternFill(start_color="FEF9C3", end_color="FEF9C3", fill_type="solid")

            elif "rojo" in color:
                fill = PatternFill(start_color="FECACA", end_color="FECACA", fill_type="solid")

            elif "gris" in color:
                fill = PatternFill(start_color="E5E7EB", end_color="E5E7EB", fill_type="solid")

            elif "rosa" in color:
                fill = PatternFill(start_color="FFC5D3", end_color="FFC5D3", fill_type="solid")

            elif "azul" in color:
                fill = PatternFill(start_color="BEDFFB", end_color="BEDFFB", fill_type="solid")

            if fill:
                for col in range(1, len(df.columns)+1):
                    ws.cell(row=row, column=col).fill = fill

    buffer.seek(0)

    return StreamingResponse(
        buffer,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={
            "Content-Disposition": f"attachment; filename=preventivos_{anio}.xlsx"
        }
    )
#################REIMPRIMIR QR###############
@router.get("/QR_REIMPRIMIR/{ubicacion}")
def reimprimir_qr(ubicacion: str):

    ruta = QR_DIR / f"{ubicacion}.png"

    if not ruta.exists():
        return {
            "success": False,
            "mensaje": "QR no existe para esa ubicación"
        }

    return {
        "success": True,
        "qr_url": f"/QR_CODES/MESAS/{ubicacion}.png"
    }


############################################
############ GENERAR QR POR UBICACION ######
############################################

@router.get("/QR_MESA_GENERAR/{ubicacion}")
def generar_qr_mesa(ubicacion: str, request: Request = None):

    ubicacion_url = quote(ubicacion)

    if request:
        base = str(request.base_url)
    else:
        base = "http://172.24.104.1:8000/"

    url = f"{base}preventivos/qr/{ubicacion_url}"

    # generar QR correctamente
    qr = qrcode.make(url).convert("RGB")

    qr_width, qr_height = qr.size

    # crear imagen con espacio abajo
    altura_total = qr_height + 60
    img = Image.new("RGB", (qr_width, altura_total), "white")

    # pegar QR
    img.paste(qr, (0, 0, qr_width, qr_height))

    draw = ImageDraw.Draw(img)

    # fuente
    try:
        font = ImageFont.truetype("arial.ttf", 22)
    except:
        font = ImageFont.load_default()

    # centrar texto
    text_width = draw.textlength(ubicacion, font=font)
    x = (qr_width - text_width) / 2
    y = qr_height + 15

    draw.text((x, y), ubicacion, fill="black", font=font)

    ruta = QR_DIR / f"{ubicacion}.png"

    img.save(ruta)

    return {
        "mensaje": "QR generado correctamente",
        "ruta_qr": str(ruta),
        "url": url
    }
#########################preventivo digital################
@router.post("/PREVENTIVO/GUARDAR_DIGITAL/{id}")
def guardar_preventivo_digital(id:int, data:dict):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    UPDATE public."MANTENIMIENTOS_PREVENTIVOS"
    SET
        "PREVENTIVO_DIGITAL" = %s,
        "FECHA_REALIZACION" = NOW()
    WHERE "Id" = %s
    """,(json.dumps(data), id))

    conn.commit()

    cursor.close()
    conn.close()

    return {"ok":True}
@router.get("/PREVENTIVO/DIGITAL/{id}")
def obtener_preventivo_digital(id:int):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    SELECT "PREVENTIVO_DIGITAL"
    FROM public."MANTENIMIENTOS_PREVENTIVOS"
    WHERE "Id"=%s
    """,(id,))

    row = cursor.fetchone()

    cursor.close()
    conn.close()

    if not row or not row[0]:
        return {"existe": False}

    data = row[0]

    # si viene como string convertir
    if isinstance(data, str):
        data = json.loads(data)

    return {
        "existe": True,
        "data": data
    }
@router.delete("/PREVENTIVO/ELIMINAR_DIGITAL/{id}")
def eliminar_preventivo_digital(id:int):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    UPDATE public."MANTENIMIENTOS_PREVENTIVOS"
    SET "PREVENTIVO_DIGITAL" = NULL
    WHERE "Id" = %s
    """,(id,))

    conn.commit()

    cursor.close()
    conn.close()

    return {"ok":True}

@router.get("/preventivos/qr/{ubicacion}", response_class=HTMLResponse)
def ver_qr_preventivo(ubicacion: str):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
    SELECT
    "Id",
    "ID_EQUIPO",
    "nombre_dispositivo",
    "PLANTA",
    "CATEGORIA_COLOR",
    "FECHA_REALIZACION",
    "PLAZO",
    "OBSERVACIONES",
    "PREVENTIVO_DIGITAL"
    FROM public."MANTENIMIENTOS_PREVENTIVOS"
    WHERE "UBICACION"=%s
    ORDER BY "nombre_dispositivo"
    """,(ubicacion,))

    rows = cursor.fetchall()

    cursor.close()
    conn.close()

    # verificar si ya existe preventivo digital en alguno
    preventivo_existe = False
    primer_id = None

    for r in rows:
        if not primer_id:
            primer_id = r[0]

        if r[8]:
            preventivo_existe = True

    # decidir botón global
    if preventivo_existe:

        boton_global = f"""
        <button class="btn_format"
        onclick="verPreventivoDigital({primer_id})">
        VER PREVENTIVO DIGITAL
        </button>

        <button class="btn_editar"
        onclick="editarPreventivo({primer_id})">
        EDITAR PREVENTIVO
        </button>

        <button class="btn_cancelar"
        onclick="eliminarPreventivo({primer_id})">
        ELIMINAR PREVENTIVO
        </button>
        """

    else:

        boton_global = f"""
        <button class="btn_format"
        onclick="abrirFormatoGlobal({primer_id})">
        GENERAR PREVENTIVO
        </button>
        """

    html = f"""
<html>

<head>

<title>MANTENIMIENTOS PREVENTIVOS</title>

<style>

body {{
font-family: Arial;
background:#eef2f7;
margin:0;
}}

header {{
background:#2c3e50;
color:white;
padding:15px 25px;
font-size:20px;
font-weight:bold;
}}

.container {{
padding:30px;
}}

.grid {{
display:grid;
grid-template-columns:repeat(auto-fit,minmax(420px,1fr));
gap:20px;
}}

.card {{
background:white;
border-radius:10px;
padding:20px;
box-shadow:0 4px 10px rgba(0,0,0,0.15);
}}

.grid_info{{
display:grid;
grid-template-columns:1fr 1fr 1fr 1fr;
gap:20px;
margin-bottom:15px;
}}

.field{{
display:flex;
flex-direction:column;
font-size:13px;
}}

.label{{
font-weight:bold;
color:#2c3e50;
margin-bottom:3px;
}}

input, textarea {{
width:100%;
border:1px solid #ccc;
border-radius:5px;
padding:6px;
font-size:13px;
}}

textarea {{
height:70px;
}}

.buttons{{
margin-top:15px;
display:flex;
gap:10px;
}}

button {{
padding:7px 14px;
border:none;
border-radius:5px;
cursor:pointer;
font-size:13px;
}}

.btn_editar {{background:#3498db;color:white}}
.btn_guardar {{background:#2ecc71;color:white}}
.btn_cancelar {{background:#e74c3c;color:white}}
.btn_salir {{background:#34495e;color:white}}
.btn_format {{background:#c6d704;color:white}}

</style>

</head>

<body>

<header>
MANTENIMIENTO PREVENTIVO
</header>

<div class="container">

<h2>UBICACION: {ubicacion}</h2>

<div style="margin-bottom:20px">
{boton_global}
</div>

<div class="grid">
"""

    for r in rows:

        id_registro = r[0]
        id_equipo = r[1] or ""
        dispositivo = r[2] or ""
        planta = r[3] or ""
        color = r[4] or ""
        fecha = r[5]
        plazo = r[6]
        obs = r[7] or ""

        html += f"""

<div class="card">

<input type="hidden" id="ubicacion_{id_registro}" value="{ubicacion}">

<div class="grid_info">

<div class="field">
<div class="label">ID EQUIPO</div>
<input id="equipo_{id_registro}" value="{id_equipo}" disabled>
</div>

<div class="field">
<div class="label">DISPOSITIVO</div>
<input id="disp_{id_registro}" value="{dispositivo}" disabled>
</div>

<div class="field">
<div class="label">PLANTA</div>
<input id="planta_{id_registro}" value="{planta}" disabled>
</div>

<div class="field">
<div class="label">COLOR</div>
<input id="color_{id_registro}" value="{color}" disabled>
</div>

</div>

<div class="grid_info">

<div class="field">
<div class="label">ULTIMO MANTENIMIENTO</div>
<input value="{fecha if fecha else 'SIN REGISTRO'}" disabled>
</div>

<div class="field">
<div class="label">PLAZO</div>
<input value="{plazo if plazo else 'NO DEFINIDO'}" disabled>
</div>

</div>

<div class="field">

<div class="label">OBSERVACIONES</div>

<textarea id="obs_{id_registro}" disabled>{obs}</textarea>

</div>

<div class="buttons">

<button class="btn_editar" onclick="abrirLogin({id_registro})">EDITAR</button>

<button class="btn_guardar" onclick="guardarCambios({id_registro})">GUARDAR</button>

<button class="btn_cancelar" onclick="location.reload()">CANCELAR</button>

<button class="btn_salir" onclick="window.close()">SALIR</button>

</div>

</div>
"""

    html += """

</div>

<script>

function abrirFormatoGlobal(id){

let usuario = prompt("Ingrese su usuario")

if(!usuario){
alert("Usuario requerido")
return
}

window.open(
"/static/formato_preventivo_virtual.html?id="+id+"&usuario="+usuario,
"_blank",
"width=1200,height=900"
)

}

function verPreventivoDigital(id){

window.open(
"/static/formato_preventivo_virtual.html?id="+id+"&modo=ver",
"_blank"
)

}

function verPreventivoDigital(id){

window.open(
"/static/formato_preventivo_virtual.html?id="+id+"&modo=ver",
"_blank"
)

}

function editarPreventivo(id){

window.open(
"/static/formato_preventivo_virtual.html?id="+id+"&modo=editar",
"_blank"
)

}
function abrirLogin(id){

let usuario = prompt("Ingrese su usuario")

if(!usuario){
alert("Usuario requerido")
return
}

document.getElementById("equipo_"+id).disabled = false
document.getElementById("disp_"+id).disabled = false
document.getElementById("planta_"+id).disabled = false
document.getElementById("color_"+id).disabled = false
document.getElementById("obs_"+id).disabled = false

}
async function eliminarPreventivo(id){

let confirmar = confirm("¿Eliminar preventivo digital?")

if(!confirmar) return

let res = await fetch("/PREVENTIVO/ELIMINAR_DIGITAL/"+id,{
method:"DELETE"
})

let data = await res.json()

if(data.ok){

alert("Preventivo eliminado")
location.reload()

}else{

alert("Error al eliminar")

}

}
async function guardarCambios(id){

let id_equipo = document.getElementById("equipo_"+id).value
let dispositivo = document.getElementById("disp_"+id).value
let planta = document.getElementById("planta_"+id).value
let color = document.getElementById("color_"+id).value
let obs = document.getElementById("obs_"+id).value
let ubicacion = document.getElementById("ubicacion_"+id).value

let datos = {
ID_EQUIPO: id_equipo,
UBICACION: ubicacion,
nombre_dispositivo: dispositivo,
PLANTA: planta,
CATEGORIA_COLOR: color,
OBSERVACIONES: obs
}

let res = await fetch("/PREVENTIVO/"+id,{
method:"PUT",
headers:{
"Content-Type":"application/json"
},
body:JSON.stringify(datos)
})

let data = await res.json()

if(data.mensaje){
alert("CAMBIOS GUARDADOS")
location.reload()
}else{
alert("ERROR AL GUARDAR")
console.log(data)
}

}
async function generarTodosQR(){

if(!confirm("Esto generará QR para TODAS las ubicaciones ¿continuar?"))
return

let res = await fetch("/QR_GENERAR_TODOS")

let data = await res.json()

alert("QR generados: " + data.total)

console.log(data)

}

</script>

</body>
</html>
"""

    return html


