import psycopg2
from psycopg2.extras import RealDictCursor


# ======================================
# CONEXION A POSTGRESQL
# ======================================

DB_HOST = "127.0.0.1"
DB_NAME = "SISTEMAS"
DB_USER = "postgres"
DB_PASSWORD = "Tristan2468"
DB_PORT = "5432"


def get_connection():
    """
    Devuelve una conexión a PostgreSQL
    """
    return psycopg2.connect(
        host=DB_HOST,
        database=DB_NAME,
        user=DB_USER,
        password=DB_PASSWORD,
        port=DB_PORT
    )


# ======================================
# CONEXION CON CURSOR EN DICCIONARIO
# (opcional pero muy util)
# ======================================

def get_dict_connection():
    """
    Devuelve conexión con cursor tipo diccionario
    """
    return psycopg2.connect(
        host=DB_HOST,
        database=DB_NAME,
        user=DB_USER,
        password=DB_PASSWORD,
        port=DB_PORT,
        cursor_factory=RealDictCursor
    )