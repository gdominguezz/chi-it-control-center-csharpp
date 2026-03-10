import os

# Carpeta base donde se guardan todos los PDFs
BASE_PDF_DIR = r"C:\PDF_DATABASE"


def get_pdf_path(modulo: str, registro_id: int):
    """
    Devuelve la ruta completa de un PDF.

    Ejemplo:
    C:\PDF_DATABASE\PREVENTIVOS\10.pdf
    """

    folder = os.path.join(BASE_PDF_DIR, modulo)

    # crear carpeta si no existe
    os.makedirs(folder, exist_ok=True)

    filename = f"{registro_id}.pdf"

    return os.path.join(folder, filename)


def pdf_exists(modulo: str, registro_id: int):
    """
    Verifica si un PDF existe
    """

    path = get_pdf_path(modulo, registro_id)

    return os.path.exists(path)


def delete_pdf(modulo: str, registro_id: int):
    """
    Elimina un PDF si existe
    """

    path = get_pdf_path(modulo, registro_id)

    if os.path.exists(path):
        os.remove(path)