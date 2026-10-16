from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field
from starlette.exceptions import HTTPException as StarletteHTTPException

app = FastAPI()

STOCK: dict[str, int] = {}


def reset_state() -> None:
    STOCK.clear()
    STOCK.update({"apple": 5, "pear": 0})


reset_state()

# TODO: AppError, NotFound, OutOfStock, обработчики исключений и эндпоинты
