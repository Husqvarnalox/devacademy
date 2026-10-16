import os
from collections.abc import Mapping
from contextlib import asynccontextmanager
from dataclasses import dataclass
from typing import Annotated

from fastapi import APIRouter, Depends, FastAPI, HTTPException, Request
from pydantic import BaseModel, Field


@dataclass(frozen=True)
class Settings:
    app_name: str = "shop"
    max_items: int = 100
    debug: bool = False


def load_settings(env: Mapping[str, str] | None = None) -> Settings:
    # TODO: читать SHOP_APP_NAME, SHOP_MAX_ITEMS (int), SHOP_DEBUG ("1"/"true"/"yes"/"on", регистр не важен)
    # из env (по умолчанию os.environ). Нечисловой SHOP_MAX_ITEMS -> ValueError с именем переменной.
    return Settings()


class ItemIn(BaseModel):
    name: str = Field(min_length=1)


class ItemOut(BaseModel):
    id: int
    name: str


def get_store(request: Request) -> dict[int, dict]:
    return request.app.state.items


def get_settings(request: Request) -> Settings:
    return request.app.state.settings


Store = Annotated[dict[int, dict], Depends(get_store)]
Config = Annotated[Settings, Depends(get_settings)]

items_router = APIRouter(prefix="/items", tags=["items"])
meta_router = APIRouter(tags=["meta"])

# TODO: POST /items (201, 409 при достижении max_items), GET /items, GET /items/{item_id} (404),
# GET /info -> {"app", "debug", "items"} на роутерах выше.


def create_app(settings: Settings | None = None) -> FastAPI:
    # TODO: на каждый вызов - НОВОЕ приложение со своим состоянием (app.state.items, app.state.settings),
    # lifespan, который ставит app.state.started=True при старте и app.state.closed=True при остановке,
    # и подключёнными роутерами. Общих изменяемых глобальных данных быть не должно.
    return FastAPI(title=(settings or Settings()).app_name)
