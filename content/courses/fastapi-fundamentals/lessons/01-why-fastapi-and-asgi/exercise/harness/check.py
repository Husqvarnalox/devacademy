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


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


from fastapi.testclient import TestClient

import api_task as m

client = TestClient(m.app)


def fresh():
    m.reset_state()


def t_health():
    r = client.get("/health")
    return expect((r.status_code, r.json()), (200, {"status": "ok"}), "GET /health")


def t_hello():
    r = client.get("/hello/Ann")
    return expect((r.status_code, r.json()), (200, {"message": "Hello, Ann!"}), "GET /hello/Ann")


def t_create_job():
    fresh()
    r = client.post("/jobs")
    return expect((r.status_code, r.json()), (201, {"id": 1, "status": "queued"}), "POST /jobs")


def t_job_sequence():
    fresh()
    a = client.post("/jobs").json()
    b = client.post("/jobs").json()
    bad = expect((a["id"], b["id"]), (1, 2), "id двух созданных задач")
    if bad:
        return bad
    r = client.get(f"/jobs/{b['id']}")
    bad = expect((r.status_code, r.json()), (200, {"id": 2, "status": "queued"}), "GET /jobs/2")
    if bad:
        return bad
    fresh()
    return expect(client.post("/jobs").json()["id"], 1, "первый id после reset_state()")


def t_not_found():
    fresh()
    r = client.get("/jobs/99")
    bad = expect((r.status_code, r.json()), (404, {"detail": "job not found"}), "GET /jobs/99")
    if bad:
        return bad
    r = client.get("/jobs/abc")
    return expect(r.status_code, 422, "GET /jobs/abc (job_id: int)")


def t_delete():
    fresh()
    jid = client.post("/jobs").json()["id"]
    r = client.delete(f"/jobs/{jid}")
    bad = expect((r.status_code, r.content), (204, b""), "DELETE существующей задачи")
    if bad:
        return bad
    bad = expect(client.get(f"/jobs/{jid}").status_code, 404, "GET после удаления")
    if bad:
        return bad
    r = client.delete(f"/jobs/{jid}")
    return expect((r.status_code, r.json()), (404, {"detail": "job not found"}), "повторный DELETE")


def t_unicode_method():
    r = client.get("/hello/Анна Мария")
    bad = expect(r.json(), {"message": "Hello, Анна Мария!"}, "GET /hello/Анна Мария")
    if bad:
        return bad
    r = client.put("/health")
    bad = expect(r.status_code, 405, "PUT /health")
    if bad:
        return bad
    return expect(client.get("/nope").status_code, 404, "GET /nope")


def t_openapi():
    paths = client.get("/openapi.json").json()["paths"]
    for p in ("/health", "/hello/{name}", "/jobs", "/jobs/{job_id}"):
        if p not in paths:
            return f"в схеме OpenAPI нет пути {p}"
    if "201" not in paths["/jobs"]["post"]["responses"]:
        return "POST /jobs должен быть описан с ответом 201"
    if "204" not in paths["/jobs/{job_id}"]["delete"]["responses"]:
        return "DELETE /jobs/{job_id} должен быть описан с ответом 204"


case("health", t_health)
case("hello", t_hello)
case("create-job", t_create_job)
case("job-sequence", t_job_sequence)
case("job-not-found", t_not_found)
case("delete-204", t_delete)
case("unicode-and-method", t_unicode_method)
case("openapi", t_openapi)
