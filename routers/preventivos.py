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

    # ── color badge por categoria ──
    def color_badge(cat):
        c = (cat or "").lower()
        if "verde"    in c: return "#10B981","#052e16","Verde"
        if "amarillo" in c: return "#F59E0B","#1c1400","Amarillo"
        if "rojo"     in c: return "#EF4444","#1f0000","Rojo"
        if "gris"     in c: return "#94A3B8","#0f172a","Gris"
        if "rosa"     in c: return "#F472B6","#1f0011","Rosa"
        if "azul"     in c: return "#3B82F6","#001233","Azul"
        return "#64748B","#0f172a", cat or "—"

    def disp_icon(disp):
        d = (disp or "").upper()
        if "COMPUTADORA" in d or "CPU" in d: return "🖥️"
        if "PORTATIL" in d or "LAPTOP" in d:  return "💻"
        if "IMPRESORA" in d:                  return "🖨️"
        if "UPS" in d:                        return "🔋"
        return "🔧"

    html = f"""<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>PM — {ubicacion}</title>
<link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@300;400;500;600;700&family=DM+Mono:wght@400;500&display=swap" rel="stylesheet">
<style>
:root{{
  --bg:#0B0F1A;--surface:#111827;--surface2:#1a2235;
  --border:rgba(255,255,255,0.07);--border2:rgba(255,255,255,0.12);
  --accent:#3B82F6;--text:#F1F5F9;--muted:#64748B;--muted2:#94A3B8;
  --green:#10B981;--red:#EF4444;--amber:#F59E0B;
  --radius:14px;
}}
*{{box-sizing:border-box;margin:0;padding:0;}}
body{{font-family:'DM Sans',sans-serif;background:var(--bg);color:var(--text);min-height:100vh;padding-bottom:40px;}}

/* HEADER */
.top-bar{{
  background:linear-gradient(135deg,#0f1e35,#0B0F1A);
  border-bottom:1px solid var(--border2);
  padding:16px 20px;
  display:flex;align-items:center;gap:14px;
  position:sticky;top:0;z-index:100;
}}
.top-icon{{
  width:42px;height:42px;border-radius:10px;
  background:linear-gradient(135deg,#1D4ED8,#3B82F6);
  display:flex;align-items:center;justify-content:center;
  font-size:20px;flex-shrink:0;
  box-shadow:0 0 16px rgba(59,130,246,.4);
}}
.top-title{{flex:1;}}
.top-title h1{{font-size:15px;font-weight:700;letter-spacing:-.01em;}}
.top-title p{{font-size:11px;color:var(--muted2);margin-top:2px;font-family:'DM Mono',monospace;}}

/* ACTION BUTTONS (global) */
.global-actions{{
  padding:16px 20px 8px;
  display:flex;flex-wrap:wrap;gap:8px;
}}
.btn{{
  display:inline-flex;align-items:center;gap:6px;
  padding:10px 16px;border:none;border-radius:8px;
  font-family:'DM Sans',sans-serif;font-size:13px;font-weight:600;
  cursor:pointer;transition:all .2s;letter-spacing:.01em;
}}
.btn-primary{{background:var(--accent);color:white;box-shadow:0 4px 12px rgba(59,130,246,.35);}}
.btn-primary:hover{{background:#2563EB;transform:translateY(-1px);}}
.btn-success{{background:var(--green);color:white;box-shadow:0 4px 12px rgba(16,185,129,.3);}}
.btn-success:hover{{background:#059669;transform:translateY(-1px);}}
.btn-danger{{background:var(--red);color:white;box-shadow:0 4px 12px rgba(239,68,68,.3);}}
.btn-danger:hover{{background:#DC2626;transform:translateY(-1px);}}
.btn-amber{{background:var(--amber);color:#1c1400;box-shadow:0 4px 12px rgba(245,158,11,.3);}}
.btn-amber:hover{{background:#D97706;transform:translateY(-1px);}}
.btn-ghost{{background:var(--surface2);color:var(--muted2);border:1px solid var(--border2);}}
.btn-ghost:hover{{color:var(--text);border-color:var(--accent);}}

/* GRID */
.grid{{
  display:grid;
  grid-template-columns:repeat(auto-fill,minmax(320px,1fr));
  gap:14px;
  padding:8px 20px 20px;
}}

/* CARD */
.card{{
  background:var(--surface);
  border:1px solid var(--border);
  border-radius:var(--radius);
  overflow:hidden;
  transition:border-color .2s,transform .2s;
  animation:fadeUp .35s ease both;
}}
.card:hover{{border-color:var(--border2);transform:translateY(-2px);}}
@keyframes fadeUp{{from{{opacity:0;transform:translateY(12px)}}to{{opacity:1;transform:translateY(0)}}}}

/* CARD TOP BAR */
.card-top{{
  display:flex;align-items:center;gap:12px;
  padding:14px 16px;
  border-bottom:1px solid var(--border);
  background:var(--surface2);
}}
.dev-icon{{
  width:38px;height:38px;border-radius:9px;
  background:rgba(59,130,246,.12);border:1px solid rgba(59,130,246,.2);
  display:flex;align-items:center;justify-content:center;font-size:18px;flex-shrink:0;
}}
.dev-name{{flex:1;}}
.dev-name h3{{font-size:13px;font-weight:700;color:var(--text);}}
.dev-name span{{font-size:11px;color:var(--muted2);font-family:'DM Mono',monospace;}}
.color-badge{{
  padding:4px 10px;border-radius:999px;
  font-size:10px;font-weight:700;
  letter-spacing:.06em;text-transform:uppercase;
}}

/* CARD BODY */
.card-body{{padding:14px 16px;}}
.info-row{{
  display:grid;grid-template-columns:1fr 1fr;
  gap:10px;margin-bottom:12px;
}}
.info-item label{{
  font-size:9px;font-weight:600;text-transform:uppercase;
  letter-spacing:.1em;color:var(--muted);display:block;margin-bottom:4px;
}}
.info-item input{{
  width:100%;background:var(--surface2);
  border:1px solid var(--border2);border-radius:6px;
  padding:7px 9px;font-size:12px;font-family:'DM Mono',monospace;
  color:var(--text);transition:border-color .2s;
}}
.info-item input:disabled{{opacity:.6;cursor:default;}}
.info-item input:not(:disabled){{
  border-color:var(--accent);background:rgba(59,130,246,.08);
  box-shadow:0 0 0 3px rgba(59,130,246,.12);
}}
.info-item input:focus{{outline:none;}}

.obs-label{{
  font-size:9px;font-weight:600;text-transform:uppercase;
  letter-spacing:.1em;color:var(--muted);margin-bottom:4px;
}}
.obs-field{{
  width:100%;background:var(--surface2);
  border:1px solid var(--border2);border-radius:6px;
  padding:8px 10px;font-size:12px;font-family:'DM Sans',sans-serif;
  color:var(--text);resize:vertical;min-height:64px;
  transition:border-color .2s;
}}
.obs-field:disabled{{opacity:.6;cursor:default;}}
.obs-field:not(:disabled){{
  border-color:var(--accent);background:rgba(59,130,246,.08);
  box-shadow:0 0 0 3px rgba(59,130,246,.12);
}}
.obs-field:focus{{outline:none;}}

/* STATUS ROW */
.status-row{{
  display:flex;align-items:center;gap:8px;
  padding:9px 12px;
  background:rgba(255,255,255,.03);
  border-top:1px solid var(--border);
  border-bottom:1px solid var(--border);
  margin:12px -0px;
  font-size:11px;color:var(--muted2);
}}
.status-dot{{width:7px;height:7px;border-radius:50%;flex-shrink:0;}}
.dot-ok{{background:var(--green);box-shadow:0 0 6px var(--green);}}
.dot-warn{{background:var(--amber);box-shadow:0 0 6px var(--amber);}}
.dot-none{{background:var(--muted);}}

/* CARD ACTIONS */
.card-actions{{
  display:flex;flex-wrap:wrap;gap:6px;
  padding:12px 16px;
  border-top:1px solid var(--border);
  background:rgba(0,0,0,.15);
}}
.card-actions .btn{{font-size:11px;padding:7px 12px;}}
</style>
</head>
<body>

<div class="top-bar">
  <div class="top-icon">🔧</div>
  <div class="top-title">
    <h1>Mantenimiento Preventivo</h1>
    <p>📍 {ubicacion}</p>
  </div>
</div>

<div class="global-actions">
{boton_global}
</div>

<div class="grid">
"""

    for r in rows:
        id_registro = r[0]
        id_equipo   = r[1] or ""
        dispositivo = r[2] or ""
        planta      = r[3] or ""
        color_cat   = r[4] or ""
        fecha       = r[5]
        plazo       = r[6]
        obs         = r[7] or ""

        badge_color, badge_bg, badge_label = color_badge(color_cat)
        icon = disp_icon(dispositivo)
        fecha_str = str(fecha)[:10] if fecha else "Sin registro"
        plazo_str = plazo if plazo else "No definido"

        # status dot
        if fecha:
            dot_class = "dot-ok"
            dot_label = f"Último PM: {fecha_str}"
        else:
            dot_class = "dot-warn"
            dot_label = "Sin mantenimiento registrado"

        html += f"""
<div class="card">
  <input type="hidden" id="ubicacion_{{id_registro}}" value="{ubicacion}">

  <div class="card-top">
    <div class="dev-icon">{icon}</div>
    <div class="dev-name">
      <h3>{dispositivo or "Dispositivo"}</h3>
      <span>{id_equipo}</span>
    </div>
    <span class="color-badge" style="background:{badge_bg};color:{badge_color};border:1px solid {badge_color}40">{badge_label}</span>
  </div>

  <div class="card-body">
    <div class="info-row">
      <div class="info-item">
        <label>ID Equipo</label>
        <input id="equipo_{id_registro}" value="{id_equipo}" disabled>
      </div>
      <div class="info-item">
        <label>Dispositivo</label>
        <input id="disp_{id_registro}" value="{dispositivo}" disabled>
      </div>
      <div class="info-item">
        <label>Planta</label>
        <input id="planta_{id_registro}" value="{planta}" disabled>
      </div>
      <div class="info-item">
        <label>Color</label>
        <input id="color_{id_registro}" value="{color_cat}" disabled>
      </div>
    </div>

    <div class="status-row">
      <span class="status-dot {dot_class}"></span>
      <span>{dot_label}</span>
      <span style="margin-left:auto;font-family:'DM Mono',monospace;font-size:10px;color:#475569">Plazo: {plazo_str}</span>
    </div>

    <div style="margin-top:10px">
      <div class="obs-label">Observaciones</div>
      <textarea class="obs-field" id="obs_{id_registro}" disabled>{obs}</textarea>
    </div>
  </div>

  <div class="card-actions">
    <button class="btn btn-primary" onclick="abrirLogin({id_registro})">✏️ Editar</button>
    <button class="btn btn-success" onclick="guardarCambios({id_registro})">💾 Guardar</button>
    <button class="btn btn-ghost"   onclick="location.reload()">↩ Cancelar</button>
    <button class="btn btn-ghost"   onclick="window.close()">✕ Salir</button>
  </div>
</div>
"""

    html += """
</div>

<script>
function abrirFormatoGlobal(id){
  let usuario = prompt("Ingrese su usuario")
  if(!usuario){ alert("Usuario requerido"); return }
  window.open("/static/formato_preventivo_virtual.html?id="+id+"&usuario="+usuario,"_blank","width=1200,height=900")
}
function verPreventivoDigital(id){
  window.open("/static/formato_preventivo_virtual.html?id="+id+"&modo=ver","_blank")
}
function editarPreventivo(id){
  window.open("/static/formato_preventivo_virtual.html?id="+id+"&modo=editar","_blank")
}
function abrirLogin(id){
  let usuario = prompt("Ingrese su usuario")
  if(!usuario){ alert("Usuario requerido"); return }
  document.getElementById("equipo_"+id).disabled = false
  document.getElementById("disp_"+id).disabled   = false
  document.getElementById("planta_"+id).disabled = false
  document.getElementById("color_"+id).disabled  = false
  document.getElementById("obs_"+id).disabled    = false
}
async function eliminarPreventivo(id){
  if(!confirm("¿Eliminar preventivo digital?")) return
  let res  = await fetch("/PREVENTIVO/ELIMINAR_DIGITAL/"+id,{method:"DELETE"})
  let data = await res.json()
  if(data.ok){ alert("Preventivo eliminado"); location.reload() }
  else alert("Error al eliminar")
}
async function guardarCambios(id){
  let datos = {
    ID_EQUIPO:          document.getElementById("equipo_"+id).value,
    UBICACION:          document.getElementById("ubicacion_"+id).value,
    nombre_dispositivo: document.getElementById("disp_"+id).value,
    PLANTA:             document.getElementById("planta_"+id).value,
    CATEGORIA_COLOR:    document.getElementById("color_"+id).value,
    OBSERVACIONES:      document.getElementById("obs_"+id).value
  }
  let res  = await fetch("/PREVENTIVO/"+id,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(datos)})
  let data = await res.json()
  if(data.mensaje){ alert("CAMBIOS GUARDADOS"); location.reload() }
  else{ alert("ERROR AL GUARDAR"); console.log(data) }
}
</script>
</body>
</html>
"""

    return html