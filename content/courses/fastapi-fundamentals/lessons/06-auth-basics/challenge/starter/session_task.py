import secrets
import time
from typing import Annotated

from fastapi import Depends, FastAPI, HTTPException
from fastapi.security import OAuth2PasswordBearer

app = FastAPI()
oauth2_scheme = OAuth2PasswordBearer(tokenUrl="session")
TTL = 60


class TokenStore:
    def __init__(self) -> None:
        self._tokens: dict[str, tuple[str, float]] = {}

    def issue(self, user: str, ttl: float, now: float) -> str:
        # TODO: выдать токен и запомнить (user, now + ttl)
        raise NotImplementedError

    def resolve(self, token: str, now: float) -> str | None:
        # TODO: вернуть пользователя, пока now < expires_at; просроченное удалить
        raise NotImplementedError

    def revoke(self, token: str) -> None:
        # TODO
        raise NotImplementedError

    def revoke_user(self, user: str) -> int:
        # TODO: отозвать все токены пользователя, вернуть их число
        raise NotImplementedError

    def __len__(self) -> int:
        return len(self._tokens)


STORE = TokenStore()


def get_now() -> float:
    return time.time()


# TODO: POST /session/{user} и GET /whoami (время только через Depends(get_now))
