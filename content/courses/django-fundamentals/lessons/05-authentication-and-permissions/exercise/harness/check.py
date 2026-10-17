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


import access_app as m
from django.contrib.auth.models import Group, User
from django.test import Client


def reset():
    User.objects.all().delete()
    Group.objects.all().delete()


def t_signup():
    reset()
    user = m.signup("ann", "s3cret-pass")
    stored = User.objects.get(username="ann").password
    if "s3cret-pass" in stored:
        return "пароль сохранён открытым текстом"
    info = m.hash_summary(stored)
    bad = expect(info and (info["algorithm"], info["iterations"] >= 100_000, info["salt_length"] >= 8), ("pbkdf2_sha256", True, True), "формат хеша по умолчанию")
    if bad:
        return bad
    bad = expect((user.check_password("s3cret-pass"), user.check_password("wrong")), (True, False), "check_password")
    if bad:
        return bad
    other = m.signup("bob", "s3cret-pass")
    return expect(other.password != stored, True, "одинаковые пароли дают разные хеши (соль)")


def t_login_flow():
    reset()
    m.signup("ann", "s3cret-pass")
    c = Client()
    r = c.post("/login/", {"username": "ann", "password": "wrong"})
    bad = expect((r.status_code, r.json()), (401, {"error": "invalid credentials"}), "неверный пароль")
    if bad:
        return bad
    bad = expect(c.get("/profile/").status_code, 302, "профиль до входа")
    if bad:
        return bad
    r = c.post("/login/", {"username": "ann", "password": "s3cret-pass"})
    bad = expect((r.status_code, r.json()), (200, {"username": "ann"}), "вход")
    if bad:
        return bad
    bad = expect("sessionid" in c.cookies, True, "после входа выдана сессия")
    if bad:
        return bad
    bad = expect(c.get("/profile/").json(), {"username": "ann", "is_staff": False}, "профиль после входа")
    if bad:
        return bad
    r = c.post("/logout/")
    bad = expect(r.status_code, 204, "выход")
    if bad:
        return bad
    bad = expect(c.get("/profile/").status_code, 302, "профиль после выхода")
    if bad:
        return bad
    return expect(c.get("/login/").status_code, 405, "GET /login/")


def t_login_required():
    reset()
    c = Client()
    r = c.get("/profile/")
    bad = expect((r.status_code, r["Location"]), (302, "/login/?next=/profile/"), "аноним на /profile/")
    if bad:
        return bad
    user = m.signup("ann", "s3cret-pass")
    c.force_login(user)
    return expect(c.get("/profile/").json()["username"], "ann", "force_login")


def t_permission_levels():
    reset()
    user = m.signup("ann", "s3cret-pass")
    anon = Client()
    r = anon.get("/publish/")
    bad = expect((r.status_code, r["Location"]), (302, "/login/?next=/publish/"), "аноним на /publish/")
    if bad:
        return bad
    c = Client()
    c.force_login(user)
    bad = expect(c.get("/publish/").status_code, 403, "вошедший без права")
    if bad:
        return bad
    perm = m.Permission.objects.get(codename="publish_article")
    user.user_permissions.add(perm)
    c2 = Client()
    c2.force_login(User.objects.get(username="ann"))
    bad = expect((c2.get("/publish/").status_code, c2.get("/publish/").json()), (200, {"published": True}), "вошедший с правом")
    if bad:
        return bad
    root = User.objects.create_superuser("root", "root@example.com", "s3cret-pass")
    c3 = Client()
    c3.force_login(root)
    return expect(c3.get("/publish/").status_code, 200, "суперпользователь имеет все права")


def t_group():
    reset()
    g1 = m.make_editors_group()
    g2 = m.make_editors_group()
    bad = expect((g1.pk == g2.pk, Group.objects.filter(name="editors").count(), list(g1.permissions.values_list("codename", flat=True))), (True, 1, ["publish_article"]), "группа editors")
    if bad:
        return bad
    user = m.signup("ann", "s3cret-pass")
    c = Client()
    c.force_login(user)
    bad = expect(c.get("/publish/").status_code, 403, "до добавления в группу")
    if bad:
        return bad
    m.add_to_editors(user)
    fresh = User.objects.get(username="ann")
    bad = expect((fresh.has_perm("access.publish_article"), fresh.groups.filter(name="editors").exists()), (True, True), "has_perm через группу")
    if bad:
        return bad
    return expect(c.get("/publish/").status_code, 200, "права через группу действуют в следующем запросе")


def t_hash_edges():
    reset()
    good = "pbkdf2_sha256$1500000$abcdefghijkl$HASHVALUE="
    bad = expect(m.hash_summary(good), {"algorithm": "pbkdf2_sha256", "iterations": 1500000, "salt_length": 12}, "разбор строки пароля")
    if bad:
        return bad
    user = m.signup("ann", "s3cret-pass")
    user.set_unusable_password()
    user.save()
    bad = expect(m.hash_summary(User.objects.get(username="ann").password), None, "непригодный пароль ('!...')")
    if bad:
        return bad
    bad = expect(m.hash_summary("plaintext"), None, "не-хеш")
    if bad:
        return bad
    return expect(m.signup("zed", "another-pass1").check_password("another-pass1"), True, "signup ещё раз")


case("signup-hashing", t_signup)
case("login-logout-flow", t_login_flow)
case("login-required", t_login_required)
case("permission-levels", t_permission_levels)
case("group-permissions", t_group)
case("hash-summary-edges", t_hash_edges)
