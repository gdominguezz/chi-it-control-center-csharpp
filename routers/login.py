from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from datetime import datetime
import hashlib

from database import get_connection

router = APIRouter()


# ── Modelos ──────────────────────────────────────────────
class LoginRequest(BaseModel):
    usuario: str
    password: str


class CambioPasswordRequest(BaseModel):
    usuario: str
    password_actual: str
    password_nuevo: str


# ── Hash SHA256 ──────────────────────────────────────────
def hash_password(password: str) -> str:
    return hashlib.sha256(password.encode()).hexdigest()


# ── POST /LOGIN ──────────────────────────────────────────
@router.post("/LOGIN")
def login(data: LoginRequest):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
        SELECT id,usuario,nombre,rol,password_hash,password_temporal,activo
        FROM public.usuarios
        WHERE usuario = %s
    """, (data.usuario.upper(),))

    row = cursor.fetchone()

    if not row:
        cursor.close()
        conn.close()
        return {"ok": False, "mensaje": "Usuario o contraseña incorrectos"}

    id_, usuario, nombre, rol, pwd_hash, pwd_temporal, activo = row

    if not activo:
        cursor.close()
        conn.close()
        return {"ok": False, "mensaje": "Usuario desactivado"}

    if hash_password(data.password) != pwd_hash:
        cursor.close()
        conn.close()
        return {"ok": False, "mensaje": "Usuario o contraseña incorrectos"}

    # actualizar último acceso
    cursor.execute(
        'UPDATE public.usuarios SET ultimo_acceso=%s WHERE id=%s',
        (datetime.now(), id_)
    )

    conn.commit()

    cursor.close()
    conn.close()

    # crear respuesta
    response = JSONResponse({
        "ok": True,
        "usuario": usuario,
        "nombre": nombre,
        "rol": rol,
        "password_temporal": pwd_temporal
    })

    # 🔹 guardar usuario en cookie
    response.set_cookie(
        key="usuario",
        value=usuario,
        httponly=True
    )

    return response


# ── POST /CAMBIAR_PASSWORD ───────────────────────────────
@router.post("/CAMBIAR_PASSWORD")
def cambiar_password(data: CambioPasswordRequest):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
        SELECT id,password_hash
        FROM public.usuarios
        WHERE usuario = %s
    """, (data.usuario.upper(),))

    row = cursor.fetchone()

    if not row:
        cursor.close()
        conn.close()
        return {"ok": False, "mensaje": "Usuario no encontrado"}

    id_, pwd_hash = row

    if hash_password(data.password_actual) != pwd_hash:
        cursor.close()
        conn.close()
        return {"ok": False, "mensaje": "Contraseña actual incorrecta"}

    if len(data.password_nuevo) < 6:
        cursor.close()
        conn.close()
        return {"ok": False, "mensaje": "La nueva contraseña debe tener al menos 6 caracteres"}

    nuevo_hash = hash_password(data.password_nuevo)

    cursor.execute("""
        UPDATE public.usuarios
        SET password_hash=%s, password_temporal=false
        WHERE id=%s
    """, (nuevo_hash, id_))

    conn.commit()

    cursor.close()
    conn.close()

    return {"ok": True, "mensaje": "Contraseña actualizada correctamente"}


# ── GET /obtener-usuario ─────────────────────────────────
@router.get("/obtener-usuario")
def obtener_usuario(request: Request):

    usuario = request.cookies.get("usuario")

    if not usuario:
        return {"usuario": "SISTEMA"}

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("""
        SELECT nombre
        FROM public.usuarios
        WHERE usuario=%s
    """, (usuario,))

    row = cursor.fetchone()

    cursor.close()
    conn.close()

    if row:
        return {"usuario": row[0]}

    return {"usuario": "SISTEMA"}