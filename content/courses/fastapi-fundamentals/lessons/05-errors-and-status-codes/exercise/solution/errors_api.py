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


class AppError(Exception):
    status_code = 400
    code = "app_error"

    def __init__(self, message: str) -> None:
        super().__init__(message)
        self.message = message


class NotFound(AppError):
    status_code = 404
    code = "not_found"


class OutOfStock(AppError):
    status_code = 409
    code = "out_of_stock"


def error_body(code: str, message: str, **extra) -> dict:
    return {"error": {"code": code, "message": message, **extra}}


@app.exception_handler(AppError)
async def app_error_handler(request: Request, exc: AppError):
    return JSONResponse(status_code=exc.status_code, content=error_body(exc.code, exc.message))


@app.exception_handler(StarletteHTTPException)
async def http_error_handler(request: Request, exc: StarletteHTTPException):
    return JSONResponse(
        status_code=exc.status_code,
        content=error_body(f"http_{exc.status_code}", str(exc.detail)),
        headers=exc.headers,
    )


@app.exception_handler(RequestValidationError)
async def validation_handler(request: Request, exc: RequestValidationError):
    details = [
        {"field": ".".join(str(p) for p in err["loc"]), "message": err["msg"]}
        for err in exc.errors()
    ]
    return JSONResponse(
        status_code=422,
        content=error_body("validation_error", "Invalid request", details=details),
    )


@app.exception_handler(Exception)
async def unhandled_handler(request: Request, exc: Exception):
    return JSONResponse(status_code=500, content=error_body("internal_error", "Internal server error"))


class OrderIn(BaseModel):
    item: str
    qty: int = Field(ge=1, le=10)


@app.get("/stock/{item}")
def get_stock(item: str):
    if item not in STOCK:
        raise NotFound(f"unknown item: {item}")
    return {"item": item, "qty": STOCK[item]}


@app.post("/orders", status_code=201)
def create_order(order: OrderIn):
    if order.item not in STOCK:
        raise NotFound(f"unknown item: {order.item}")
    if order.qty > STOCK[order.item]:
        raise OutOfStock(f"only {STOCK[order.item]} left")
    STOCK[order.item] -= order.qty
    return {"item": order.item, "qty": order.qty, "remaining": STOCK[order.item]}


@app.get("/crash")
def crash():
    raise RuntimeError("secret detail")
