import json
import os
import warnings

warnings.filterwarnings("ignore")

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")
    finally:
        pass


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


from fastapi.testclient import TestClient

import auth_api as m

client = TestClient(m.app)
BEARER = {"WWW-Authenticate": "Bearer"}


def fresh():
    m.reset_state()


def register(name="alice", pw="s3cret-pass"):
    return client.post("/register", json={"username": name, "password": pw})


def login(name="alice", pw="s3cret-pass"):
    return client.post("/login", json={"username": name, "password": pw})


def auth(token):
    return {"Authorization": f"Bearer {token}"}


def admin_token():
    m.create_user("root", "admin-pass-1", role="admin")
    return login("root", "admin-pass-1").json()["access_token"]


def t_register():
    fresh()
    r = register()
    bad = expect(r.status_code, 201, "POST /register")
    if bad:
        return bad
    bad = expect(r.json(), {"id": 1, "username": "alice", "role": "user"}, "тело регистрации")
    if bad:
        return bad
    r = client.post("/register", json={"username": "bob", "password": "longenough", "role": "admin"})
    bad = expect((r.status_code, r.json().get("role")), (201, "user"), "роль из тела должна игнорироваться")
    if bad:
        return bad
    bad = expect(register().status_code, 409, "повторное имя")
    if bad:
        return bad
    bad = expect(client.post("/register", json={"username": "ab", "password": "longenough"}).status_code, 422, "короткое имя")
    if bad:
        return bad
    bad = expect(client.post("/register", json={"username": "carol", "password": "short"}).status_code, 422, "короткий пароль")
    if bad:
        return bad
    stored = m.USERS[1]["password_hash"]
    if "s3cret-pass" in stored or "s3cret-pass" in str(m.USERS):
        return "пароль в открытом виде попал в хранилище"
    for uid in m.USERS:
        resp = client.get("/me", headers=auth(login(m.USERS[uid]["username"], "s3cret-pass" if uid == 1 else "longenough").json().get("access_token", "x")))
        text = resp.text
        if "password" in text or "scrypt" in text:
            return f"/me раскрывает пароль или хеш: {text}"


def t_login_me():
    fresh()
    register()
    r = login()
    bad = expect(r.status_code, 200, "POST /login")
    if bad:
        return bad
    body = r.json()
    bad = expect(sorted(body), ["access_token", "token_type"], "ключи ответа входа")
    if bad:
        return bad
    bad = expect(body["token_type"], "bearer", "token_type")
    if bad:
        return bad
    if body["access_token"] not in m.TOKENS:
        return "выданный токен не сохранён в TOKENS"
    r = client.get("/me", headers=auth(body["access_token"]))
    return expect((r.status_code, r.json()), (200, {"id": 1, "username": "alice", "role": "user"}), "GET /me")


def t_unauthorized():
    fresh()
    register()
    r = client.get("/me")
    bad = expect(r.status_code, 401, "/me без токена")
    if bad:
        return bad
    r = client.get("/me", headers=auth("not-a-real-token"))
    bad = expect(r.status_code, 401, "/me с неизвестным токеном")
    if bad:
        return bad
    bad = expect(r.headers.get("www-authenticate"), "Bearer", "WWW-Authenticate")
    if bad:
        return bad
    bad = expect(r.json(), {"detail": "invalid token"}, "тело 401")
    if bad:
        return bad
    return expect(client.post("/logout").status_code, 401, "/logout без токена")


def t_login_failures():
    fresh()
    register()
    wrong = login("alice", "wrong-password")
    ghost = login("nobody", "whatever-pass")
    for name, r in (("неверный пароль", wrong), ("неизвестный пользователь", ghost)):
        bad = expect(r.status_code, 401, name)
        if bad:
            return bad
        bad = expect(r.json(), {"detail": "invalid credentials"}, f"тело при: {name}")
        if bad:
            return bad
        bad = expect(r.headers.get("www-authenticate"), "Bearer", f"WWW-Authenticate при: {name}")
        if bad:
            return bad
    if m.TOKENS:
        return "при неудачном входе токен не должен выдаваться"


def t_hash_format():
    h1 = m.hash_password("correct horse")
    h2 = m.hash_password("correct horse")
    if h1 == h2:
        return "у двух вызовов с одним паролем должны быть разные соли"
    parts = h1.split("$")
    bad = expect(parts[:4], ["scrypt", "16384", "8", "1"], "параметры в строке хеша")
    if bad:
        return bad
    if len(parts) != 6:
        return f"ожидалось 6 частей, получено {len(parts)}"
    bad = expect((len(bytes.fromhex(parts[4])), len(bytes.fromhex(parts[5]))), (16, 32), "длины соли и хеша в байтах")
    if bad:
        return bad
    if "correct horse" in h1:
        return "пароль не должен входить в строку хеша"
    if not m.verify_password("correct horse", h1):
        return "verify_password отклонил верный пароль"
    if m.verify_password("correct horsE", h1):
        return "verify_password принял неверный пароль"
    import hashlib
    expected = hashlib.scrypt(b"correct horse", salt=bytes.fromhex(parts[4]), n=16384, r=8, p=1, dklen=32).hex()
    return expect(parts[5], expected, "значение хеша scrypt")


def t_verify_robust():
    for junk in ("", "plain", "scrypt$1$2", "scrypt$16384$8$1$zz$zz", "md5$16384$8$1$00$00", "a$b$c$d$e$f", "scrypt$16384$8$1$00$"):
        try:
            res = m.verify_password("x", junk)
        except Exception as exc:
            return f"verify_password({junk!r}) бросил {type(exc).__name__}"
        if res is not False:
            return f"verify_password({junk!r}) должен вернуть False, получено {res!r}"


def t_roles():
    fresh()
    register("alice")
    user_tok = login("alice").json()["access_token"]
    bad = expect(client.get("/admin/users").status_code, 401, "admin без токена")
    if bad:
        return bad
    r = client.get("/admin/users", headers=auth(user_tok))
    bad = expect((r.status_code, r.json()), (403, {"detail": "admin only"}), "admin для обычного пользователя")
    if bad:
        return bad
    r = client.get("/admin/users", headers=auth("bogus"))
    bad = expect(r.status_code, 401, "admin с неверным токеном (401, а не 403)")
    if bad:
        return bad
    tok = admin_token()
    r = client.get("/admin/users", headers=auth(tok))
    bad = expect(r.status_code, 200, "admin для администратора")
    if bad:
        return bad
    bad = expect(sorted(u["username"] for u in r.json()), ["alice", "root"], "список пользователей")
    if bad:
        return bad
    if "password" in r.text or "scrypt" in r.text:
        return "список пользователей раскрывает пароль или хеш"


def t_logout():
    fresh()
    register()
    t1 = login().json()["access_token"]
    t2 = login().json()["access_token"]
    if t1 == t2:
        return "два входа выдали одинаковый токен"
    if len(t1) < 40:
        return f"токен слишком короткий ({len(t1)}): используйте secrets.token_urlsafe(32)"
    bad = expect(client.post("/logout", headers=auth(t1)).status_code, 204, "POST /logout")
    if bad:
        return bad
    bad = expect(client.get("/me", headers=auth(t1)).status_code, 401, "/me после logout")
    if bad:
        return bad
    bad = expect(client.get("/me", headers=auth(t2)).status_code, 200, "другой токен после logout первого")
    if bad:
        return bad
    r = client.post("/logout", headers=auth(t1))
    return expect(r.status_code, 401, "повторный logout тем же токеном")


def t_openapi():
    spec = client.get("/openapi.json").json()
    schemes = spec.get("components", {}).get("securitySchemes", {})
    if not any(s.get("type") == "oauth2" for s in schemes.values()):
        return f"в OpenAPI нет схемы oauth2: {schemes}"
    me = spec["paths"]["/me"]["get"]
    if not me.get("security"):
        return "у GET /me нет требований безопасности в OpenAPI"
    if spec["paths"]["/register"]["post"].get("security"):
        return "у POST /register не должно быть требований безопасности"


case("register-no-leak", t_register)
case("login-and-me", t_login_me)
case("unauthorized", t_unauthorized)
case("login-failures", t_login_failures)
case("hash-format", t_hash_format)
case("verify-robust", t_verify_robust)
case("roles", t_roles)
case("logout-revokes", t_logout)
case("openapi-security", t_openapi)
