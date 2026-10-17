import json
import os
import warnings

warnings.filterwarnings("ignore")

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


from django.test import Client

import signup_forms as m

VALID = {"username": "Alice_1", "email": "Ann@Example.com", "age": "30", "password": "s3cretpass", "password2": "s3cretpass"}


def fresh():
    m.Member.objects.all().delete()
    client = Client(enforce_csrf_checks=True)
    client.get("/register/")  # получаем CSRF-cookie
    return client


def post(client, data):
    token = client.cookies["csrftoken"].value
    r = client.post("/register/", data, headers={"X-CSRFToken": token})
    try:
        body = r.json()
    except Exception:
        body = None
    return r.status_code, body


def errors_of(data):
    status, body = post(fresh(), data)
    if status != 400:
        return f"ожидался статус 400, получен {status}: {body}"
    return body["errors"]


def t_valid():
    client = fresh()
    status, body = post(client, {"username": "  Alice_1 ", "email": "Ann@Example.com", "age": "30", "password": "s3cretpass", "password2": "s3cretpass"})
    bad = expect((status, body["username"] if body else None), (201, "alice_1"), "успешная регистрация: статус и нормализованный username")
    if bad:
        return bad
    member = m.Member.objects.get()
    return expect((member.username, member.email, member.age, member.pk == body["id"]), ("alice_1", "ann@example.com", 30, True), "сохранённый Member")


def t_rules():
    got = errors_of({"username": "9lives", "email": "nope", "age": "17", "password": "abc", "password2": "abc"})
    bad = expect(got, {"username": ["invalid_username"], "email": ["invalid"], "age": ["too_young"], "password": ["too_short", "no_digit"]}, "ошибки полей")
    if bad:
        return bad
    got = errors_of({**VALID, "password": "longpassword", "password2": "longpassword"})
    bad = expect(got, {"password": ["no_digit"]}, "пароль без цифры")
    if bad:
        return bad
    got = errors_of({**VALID, "password": "abc1", "password2": "abc1"})
    bad = expect(got, {"password": ["too_short"]}, "короткий пароль с цифрой")
    if bad:
        return bad
    return expect(m.Member.objects.count(), 0, "при ошибках Member не создаётся")


def t_mismatch():
    got = errors_of({**VALID, "password2": "s3cretpasX"})
    bad = expect(got, {"password2": ["password_mismatch"]}, "разные пароли")
    if bad:
        return bad
    data = {k: v for k, v in VALID.items() if k != "password2"}
    return expect(errors_of(data), {"password2": ["required"]}, "password2 не передан")


def t_csrf():
    m.Member.objects.all().delete()
    strict = Client(enforce_csrf_checks=True)
    r = strict.post("/register/", VALID)
    bad = expect(r.status_code, 403, "POST без CSRF-токена")
    if bad:
        return bad
    r = strict.get("/register/")
    bad = expect((r.status_code, "csrftoken" in strict.cookies), (200, True), "GET выдаёт CSRF-cookie (ensure_csrf_cookie)")
    if bad:
        return bad
    bad = expect(m.Member.objects.count(), 0, "отклонённый POST ничего не создаёт")
    if bad:
        return bad
    token = strict.cookies["csrftoken"].value
    r = strict.post("/register/", {**VALID, "csrfmiddlewaretoken": token})
    bad = expect(r.status_code, 201, "POST с токеном в поле формы")
    if bad:
        return bad
    r = strict.post("/register/", {**VALID, "username": "second"}, headers={"X-CSRFToken": "wrong"})
    return expect(r.status_code, 403, "POST с неверным токеном")


def t_unique():
    client = fresh()
    status, _ = post(client, VALID)
    bad = expect(status, 201, "первая регистрация")
    if bad:
        return bad
    status, body = post(client, {**VALID, "username": "ALICE_1"})
    bad = expect((status, body), (400, {"errors": {"username": ["unique"]}}), "то же имя в другом регистре")
    if bad:
        return bad
    return expect(m.Member.objects.count(), 1, "дубликат не сохраняется")


def t_edges():
    got = errors_of({"username": "a" * 25, "email": "a@b.co", "age": "abc", "password": "s3cretpass", "password2": "s3cretpass"})
    bad = expect(got, {"username": ["max_length"], "age": ["invalid"]}, "слишком длинное имя и нецелый возраст")
    if bad:
        return bad
    bad = expect(errors_of({}), {f: ["required"] for f in ("username", "email", "age", "password", "password2")}, "пустой POST")
    if bad:
        return bad
    client = fresh()
    status, body = post(client, {**VALID, "age": "18"})
    bad = expect(status, 201, "возраст 18 допустим")
    if bad:
        return bad
    bad = expect(client.get("/register/").content.decode(), "register form", "GET /register/")
    if bad:
        return bad
    bad = expect(client.delete("/register/", headers={"X-CSRFToken": client.cookies["csrftoken"].value}).status_code, 405, "DELETE")
    if bad:
        return bad
    form = m.RegistrationForm({**VALID, "username": "ok_name", "age": "40"})
    bad = expect(form.is_valid(), True, "форма сама по себе валидна")
    if bad:
        return bad
    return expect(form.cleaned_data["email"], "ann@example.com", "cleaned_data['email'] в нижнем регистре")


case("valid-registration", t_valid)
case("field-rules", t_rules)
case("password-mismatch", t_mismatch)
case("csrf-enforced", t_csrf)
case("unique-username", t_unique)
case("edge-cases", t_edges)
