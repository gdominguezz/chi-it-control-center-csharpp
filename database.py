import psycopg2
import psycopg2.pool
from contextlib import contextmanager

# ==============================
# CONFIGURACIÓN
# ==============================
DB_HOST     = "host.docker.internal"
DB_NAME     = "SISTEMAS"
DB_USER     = "postgres"
DB_PASSWORD = "Tristan2468"
DB_PORT     = "5432"

# ==============================
# POOL GLOBAL (LAZY)
# ==============================
_pool = None

def get_pool():
    global _pool
    if _pool is None:
        print("🔌 Creando pool de conexiones...")
        _pool = psycopg2.pool.ThreadedConnectionPool(
            minconn=1,
            maxconn=5,  # 🔥 IMPORTANTE: evita saturación
            host=DB_HOST,
            database=DB_NAME,
            user=DB_USER,
            password=DB_PASSWORD,
            port=DB_PORT
        )
    return _pool

# ==============================
# CONTEXT MANAGER (USAR SIEMPRE)
# ==============================
@contextmanager
def db_conn():
    pool = get_pool()
    conn = pool.getconn()
    try:
        yield conn
    except Exception as e:
        try:
            conn.rollback()
        except Exception:
            pass
        raise e
    finally:
        try:
            pool.putconn(conn)
        except Exception:
            pass

# ==============================
# OPCIONAL: CERRAR POOL (APAGADO)
# ==============================
def close_pool():
    global _pool
    if _pool:
        print("🔌 Cerrando pool...")
        _pool.closeall()
        _pool = None

def get_connection():
    """
    Compatibilidad con código existente.
    """
    return get_pool().getconn()

def release_connection(conn):
    """
    Compatibilidad: reemplaza conn.close()
    """
    try:
        get_pool().putconn(conn)
    except Exception:
        pass