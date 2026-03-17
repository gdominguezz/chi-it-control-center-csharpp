import psycopg2
import psycopg2.pool
from psycopg2.extras import RealDictCursor
from contextlib import contextmanager

DB_HOST     = "127.0.0.1"
DB_NAME     = "SISTEMAS"
DB_USER     = "postgres"
DB_PASSWORD = "Tristan2468"
DB_PORT     = "5432"

_pool = psycopg2.pool.ThreadedConnectionPool(
    minconn=1,
    maxconn=10,
    host=DB_HOST,
    database=DB_NAME,
    user=DB_USER,
    password=DB_PASSWORD,
    port=DB_PORT
)

def get_connection():
    return _pool.getconn()

@contextmanager
def db_conn():
    conn = _pool.getconn()
    try:
        yield conn
    except Exception:
        try:
            conn.rollback()
        except Exception:
            pass
        raise
    finally:
        try:
            _pool.putconn(conn)
        except Exception:
            pass