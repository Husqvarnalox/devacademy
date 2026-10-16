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


class UserCreate(BaseModel):
    username: str = Field(min_length=3, max_length=20)
    password: str = Field(min_length=8)


class UserOut(BaseModel):
    id: int
    username: str


@app.post("/users", status_code=201, response_model=UserOut)
def create_user(body: UserCreate):
    global _next_id
    if any(u["username"] == body.username for u in USERS.values()):
        raise HTTPException(status_code=409, detail="username taken")
    record = {"id": _next_id, "username": body.username, "password_hash": _fake_hash(body.password)}
    USERS[_next_id] = record
    _next_id += 1
    return record


@app.get("/users/{user_id}", response_model=UserOut)
def get_user(user_id: Annotated[int, Path(ge=1)]):
    if user_id not in USERS:
        raise HTTPException(status_code=404, detail="user not found")
    return USERS[user_id]


@app.get("/users", response_model=list[UserOut])
def list_users(
    q: str | None = None,
    limit: Annotated[int, Query(ge=1, le=100)] = 10,
    offset: Annotated[int, Query(ge=0)] = 0,
):
    items = sorted(USERS.values(), key=lambda u: u["id"])
    if q:
        items = [u for u in items if q.lower() in u["username"].lower()]
    return items[offset : offset + limit]
