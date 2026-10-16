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
    env = os.environ if env is None else env
    defaults = Settings()
    raw_max = env.get("SHOP_MAX_ITEMS")
    if raw_max is None:
        max_items = defaults.max_items
    else:
        try:
            max_items = int(raw_max)
        except ValueError:
            raise ValueError(f"SHOP_MAX_ITEMS must be an integer, got {raw_max!r}") from None
    return Settings(
        app_name=env.get("SHOP_APP_NAME", defaults.app_name),
        max_items=max_items,
        debug=env.get("SHOP_DEBUG", "").strip().lower() in {"1", "true", "yes", "on"},
    )


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


@items_router.post("", status_code=201, response_model=ItemOut)
def create_item(body: ItemIn, store: Store, cfg: Config):
    if len(store) >= cfg.max_items:
        raise HTTPException(409, "limit reached")
    item = {"id": len(store) + 1, "name": body.name}
    store[item["id"]] = item
    return item


@items_router.get("", response_model=list[ItemOut])
def list_items(store: Store):
    return list(store.values())


@items_router.get("/{item_id}", response_model=ItemOut)
def get_item(item_id: int, store: Store):
    if item_id not in store:
        raise HTTPException(404, "item not found")
    return store[item_id]


@meta_router.get("/info")
def info(store: Store, cfg: Config):
    return {"app": cfg.app_name, "debug": cfg.debug, "items": len(store)}


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings()

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        app.state.started = True
        yield
        app.state.closed = True

    app = FastAPI(title=settings.app_name, lifespan=lifespan)
    app.state.settings = settings
    app.state.items = {}
    app.state.started = False
    app.state.closed = False
    app.include_router(items_router)
    app.include_router(meta_router)
    return app
