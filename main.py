from fastapi import FastAPI
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse

import routers.preventivos as preventivos
import routers.login as login

app = FastAPI()

app.mount("/static", StaticFiles(directory="static"), name="static")
app.mount("/QR_CODES", StaticFiles(directory="QR_CODES"), name="QR_CODES")

@app.get("/")
def root():
    return FileResponse("static/login.html")

@app.get("/preventivos")
def pagina_preventivos():
    return FileResponse("static/preventivos.html")


app.include_router(login.router)

app.include_router(preventivos.router)