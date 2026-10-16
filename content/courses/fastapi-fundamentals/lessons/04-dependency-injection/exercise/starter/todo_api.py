from typing import Annotated

from fastapi import Depends, FastAPI, HTTPException, Query
from pydantic import BaseModel, Field

app = FastAPI()


class TodoRepo:
    def __init__(self) -> None:
        self._items: dict[int, dict] = {}
        self._next = 1

    def add(self, title: str) -> dict:
        item = {"id": self._next, "title": title, "done": False}
        self._items[self._next] = item
        self._next += 1
        return item

    def get(self, todo_id: int) -> dict | None:
        return self._items.get(todo_id)

    def list(self) -> list[dict]:
        return [self._items[k] for k in sorted(self._items)]

    def delete(self, todo_id: int) -> bool:
        return self._items.pop(todo_id, None) is not None


PROD_REPO = TodoRepo()
AUDIT: list[str] = []

# TODO: get_repo, Repo, Pagination, audit_scope и четыре эндпоинта /todos
