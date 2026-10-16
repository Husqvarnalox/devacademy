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
        token = secrets.token_urlsafe(32)
        self._tokens[token] = (user, now + ttl)
        return token

    def resolve(self, token: str, now: float) -> str | None:
        entry = self._tokens.get(token)
        if entry is None:
            return None
        user, expires_at = entry
        if now >= expires_at:
            del self._tokens[token]
            return None
        return user

    def revoke(self, token: str) -> None:
        self._tokens.pop(token, None)

    def revoke_user(self, user: str) -> int:
        doomed = [t for t, (u, _) in self._tokens.items() if u == user]
        for t in doomed:
            del self._tokens[t]
        return len(doomed)

    def __len__(self) -> int:
        return len(self._tokens)


STORE = TokenStore()


def get_now() -> float:
    return time.time()


Now = Annotated[float, Depends(get_now)]


@app.post("/session/{user}")
def create_session(user: str, now: Now):
    return {"token": STORE.issue(user, TTL, now)}


@app.get("/whoami")
def whoami(token: Annotated[str, Depends(oauth2_scheme)], now: Now):
    user = STORE.resolve(token, now)
    if user is None:
        raise HTTPException(401, "invalid token", headers={"WWW-Authenticate": "Bearer"})
    return {"user": user}
