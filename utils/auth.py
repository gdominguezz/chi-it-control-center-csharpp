from fastapi import APIRouter, Request
from database import get_connection

router = APIRouter()

def obtener_usuario_actual(nombre_usuario: str):

    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute(
        'SELECT "USUARIO","NOMBRE","ROL" FROM public."USUARIOS" WHERE "USUARIO"=%s',
        (nombre_usuario,)
    )

    row = cursor.fetchone()

    cursor.close()
    conn.close()

    if not row:
        return None

    return {
        "usuario": row[0],
        "nombre": row[1],
        "rol": row[2]
    }


@router.get("/obtener-usuario")
def obtener_usuario(request: Request):

    usuario_cookie = request.cookies.get("usuario")

    if not usuario_cookie:
        return {"usuario": "SISTEMA"}

    user = obtener_usuario_actual(usuario_cookie)

    if not user:
        return {"usuario": "SISTEMA"}

    return {
        "usuario": user["nombre"]
    }