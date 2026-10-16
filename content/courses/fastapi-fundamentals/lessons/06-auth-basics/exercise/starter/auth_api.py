import hashlib
import hmac
import secrets
from typing import Annotated

from fastapi import Depends, FastAPI, HTTPException, Response
from fastapi.security import OAuth2PasswordBearer
from pydantic import BaseModel, Field

app = FastAPI()
oauth2_scheme = OAuth2PasswordBearer(tokenUrl="login")

USERS: dict[int, dict] = {}
TOKENS: dict[str, int] = {}

N, R, P = 16384, 8, 1
BEARER = {"WWW-Authenticate": "Bearer"}


def reset_state() -> None:
    USERS.clear()
    TOKENS.clear()


def hash_password(password: str) -> str:
    # TODO: scrypt$16384$8$1$<salt hex>$<hash hex>, соль 16 байт, dklen=32
    return password  # ошибка: пароль хранится открытым текстом


def verify_password(password: str, stored: str) -> bool:
    # TODO: пересчитать хеш с параметрами из stored и сравнить через hmac.compare_digest
    return password == stored


def create_user(username: str, password: str, role: str = "user") -> dict:
    if any(u["username"] == username for u in USERS.values()):
        raise ValueError("username taken")
    uid = len(USERS) + 1
    user = {"id": uid, "username": username, "role": role, "password_hash": hash_password(password)}
    USERS[uid] = user
    return user


class UserOut(BaseModel):
    id: int
    username: str
    role: str


# TODO: модели RegisterIn / LoginIn / токена, зависимость get_current_user,
# фабрика require_role и эндпоинты /register, /login, /me, /logout, /admin/users.
