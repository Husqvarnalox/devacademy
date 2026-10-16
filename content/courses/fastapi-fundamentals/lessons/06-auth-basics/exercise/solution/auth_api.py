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
    salt = secrets.token_bytes(16)
    digest = hashlib.scrypt(password.encode(), salt=salt, n=N, r=R, p=P, dklen=32)
    return "$".join(["scrypt", str(N), str(R), str(P), salt.hex(), digest.hex()])


def verify_password(password: str, stored: str) -> bool:
    try:
        scheme, n, r, p, salt_hex, hash_hex = stored.split("$")
        if scheme != "scrypt":
            return False
        expected = bytes.fromhex(hash_hex)
        candidate = hashlib.scrypt(
            password.encode(), salt=bytes.fromhex(salt_hex),
            n=int(n), r=int(r), p=int(p), dklen=len(expected),
        )
    except (ValueError, TypeError, AttributeError):
        return False
    return hmac.compare_digest(candidate, expected)


def create_user(username: str, password: str, role: str = "user") -> dict:
    if any(u["username"] == username for u in USERS.values()):
        raise ValueError("username taken")
    uid = len(USERS) + 1
    user = {"id": uid, "username": username, "role": role, "password_hash": hash_password(password)}
    USERS[uid] = user
    return user


class RegisterIn(BaseModel):
    username: str = Field(min_length=3, max_length=20)
    password: str = Field(min_length=8)


class LoginIn(BaseModel):
    username: str
    password: str


class UserOut(BaseModel):
    id: int
    username: str
    role: str


class TokenOut(BaseModel):
    access_token: str
    token_type: str = "bearer"


def get_current_user(token: Annotated[str, Depends(oauth2_scheme)]) -> dict:
    uid = TOKENS.get(token)
    if uid is None or uid not in USERS:
        raise HTTPException(401, "invalid token", headers=BEARER)
    return USERS[uid]


CurrentUser = Annotated[dict, Depends(get_current_user)]


def require_role(role: str):
    def checker(user: CurrentUser) -> dict:
        if user["role"] != role:
            raise HTTPException(403, f"{role} only")
        return user

    return checker


@app.post("/register", status_code=201, response_model=UserOut)
def register(body: RegisterIn):
    try:
        return create_user(body.username, body.password)
    except ValueError:
        raise HTTPException(409, "username taken")


@app.post("/login", response_model=TokenOut)
def login(body: LoginIn):
    user = next((u for u in USERS.values() if u["username"] == body.username), None)
    if user is None or not verify_password(body.password, user["password_hash"]):
        raise HTTPException(401, "invalid credentials", headers=BEARER)
    token = secrets.token_urlsafe(32)
    TOKENS[token] = user["id"]
    return {"access_token": token, "token_type": "bearer"}


@app.get("/me", response_model=UserOut)
def me(user: CurrentUser):
    return user


@app.post("/logout", status_code=204)
def logout(token: Annotated[str, Depends(oauth2_scheme)], user: CurrentUser):
    TOKENS.pop(token, None)
    return Response(status_code=204)


@app.get("/admin/users", response_model=list[UserOut])
def admin_users(admin: Annotated[dict, Depends(require_role("admin"))]):
    return list(USERS.values())
