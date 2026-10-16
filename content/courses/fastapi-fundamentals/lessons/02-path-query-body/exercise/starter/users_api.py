from typing import Annotated

from fastapi import FastAPI, HTTPException, Path, Query
from pydantic import BaseModel, Field

app = FastAPI()

USERS: dict[int, dict] = {}
_next_id = 1


def _fake_hash(password: str) -> str:
    """Заглушка для урока: настоящее хеширование - в уроке про аутентификацию."""
    return "fake$" + password[::-1]


def reset_state() -> None:
    global _next_id
    USERS.clear()
    _next_id = 1


# TODO: модели UserCreate (тело) и UserOut (ответ: только id и username)
# TODO: POST /users, GET /users/{user_id}, GET /users
