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


def get_repo() -> TodoRepo:
    return PROD_REPO


Repo = Annotated[TodoRepo, Depends(get_repo)]


class Pagination:
    def __init__(
        self,
        limit: Annotated[int, Query(ge=1, le=50)] = 10,
        offset: Annotated[int, Query(ge=0)] = 0,
    ) -> None:
        self.limit = limit
        self.offset = offset


def audit_scope():
    AUDIT.append("open")
    try:
        yield AUDIT
    finally:
        AUDIT.append("close")


Audit = Annotated[list, Depends(audit_scope)]


class TodoCreate(BaseModel):
    title: str = Field(min_length=1)


class TodoOut(BaseModel):
    id: int
    title: str
    done: bool


@app.post("/todos", status_code=201, response_model=TodoOut)
def create_todo(body: TodoCreate, repo: Repo, audit: Audit):
    item = repo.add(body.title)
    audit.append(f"created:{item['id']}")
    return item


@app.get("/todos", response_model=list[TodoOut])
def list_todos(repo: Repo, page: Annotated[Pagination, Depends()]):
    return repo.list()[page.offset : page.offset + page.limit]


@app.get("/todos/{todo_id}", response_model=TodoOut)
def get_todo(todo_id: int, repo: Repo):
    item = repo.get(todo_id)
    if item is None:
        raise HTTPException(status_code=404, detail="todo not found")
    return item


@app.delete("/todos/{todo_id}", status_code=204)
def delete_todo(todo_id: int, repo: Repo, audit: Audit):
    if not repo.delete(todo_id):
        raise HTTPException(status_code=404, detail="todo not found")
    audit.append(f"deleted:{todo_id}")
