import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";

function setup() {
  const app = createApp();
  const call = (method, url, body, headers = {}) =>
    inject(app, {
      method,
      url,
      headers: body === undefined ? headers : { "content-type": "application/json", ...headers },
      payload: body === undefined ? undefined : JSON.stringify(body),
    });
  return { app, call };
}

const errorCode = (res) => res.json().error.code;

describe("Создание", () => {
  it("POST создаёт задачу: 201, Location и тело", async () => {
    const { call } = setup();
    const res = await call("POST", "/tasks", { title: "Купить хлеб" });
    expect(res.statusCode).toBe(201);
    expect(res.headers.location).toBe("/tasks/1");
    expect(res.json()).toEqual({ id: 1, title: "Купить хлеб", done: false });
    const second = await call("POST", "/tasks", { title: "Второе", done: true, id: 99, extra: "x" });
    expect(second.headers.location).toBe("/tasks/2");
    expect(second.json()).toEqual({ id: 2, title: "Второе", done: true });
  });

  it("некорректное тело - 400 validation_failed", async () => {
    const { call } = setup();
    const bodies = [{}, { title: "" }, { title: "   " }, { title: 5 }, { title: "x".repeat(101) }, { title: "ok", done: "yes" }, []];
    for (const body of bodies) {
      const res = await call("POST", "/tasks", body);
      expect([res.statusCode, errorCode(res)]).toEqual([400, "validation_failed"]);
    }
    expect((await call("GET", "/tasks")).json().total).toBe(0);
  });
});

describe("Чтение и список", () => {
  it("GET по id и 404 для неизвестного", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "a" });
    const found = await call("GET", "/tasks/1");
    expect(found.statusCode).toBe(200);
    expect(found.json()).toEqual({ id: 1, title: "a", done: false });
    for (const id of ["2", "0", "abc", "1.5", "-1"]) {
      const res = await call("GET", `/tasks/${id}`);
      expect([res.statusCode, errorCode(res)]).toEqual([404, "task_not_found"]);
    }
  });

  it("фильтры done и q", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "Купить Хлеб", done: true });
    await call("POST", "/tasks", { title: "Купить молоко" });
    await call("POST", "/tasks", { title: "Позвонить маме", done: true });
    const titles = async (query) => (await call("GET", `/tasks${query}`)).json().items.map((t) => t.title);
    expect(await titles("?done=true")).toEqual(["Купить Хлеб", "Позвонить маме"]);
    expect(await titles("?done=false")).toEqual(["Купить молоко"]);
    expect(await titles("?q=купить")).toEqual(["Купить Хлеб", "Купить молоко"]);
    expect(await titles("?q=КУПИТЬ&done=true")).toEqual(["Купить Хлеб"]);
    expect(await titles("?q=нет")).toEqual([]);
    expect((await call("GET", "/tasks?q=купить&limit=1")).json().total).toBe(2);
  });

  it("пагинация: limit, offset и total", async () => {
    const { call } = setup();
    for (let i = 1; i <= 12; i += 1) await call("POST", "/tasks", { title: `task ${i}` });
    const first = (await call("GET", "/tasks")).json();
    expect([first.items.length, first.total, first.limit, first.offset]).toEqual([10, 12, 10, 0]);
    expect(first.items[0].id).toBe(1);
    const page = (await call("GET", "/tasks?limit=5&offset=10")).json();
    expect(page.items.map((t) => t.id)).toEqual([11, 12]);
    expect([page.total, page.limit, page.offset]).toEqual([12, 5, 10]);
    const beyond = (await call("GET", "/tasks?offset=50")).json();
    expect([beyond.items, beyond.total]).toEqual([[], 12]);
  });

  it("неверные параметры запроса - 400 invalid_query", async () => {
    const { call } = setup();
    const bad = ["limit=0", "limit=51", "limit=abc", "limit=1.5", "limit=", "offset=-1", "offset=x", "done=maybe", "done=1", "q=a&q=b", "limit=1&limit=2"];
    for (const query of bad) {
      const res = await call("GET", `/tasks?${query}`);
      expect([query, res.statusCode, res.statusCode === 400 && errorCode(res)]).toEqual([query, 400, "invalid_query"]);
    }
    expect((await call("GET", "/tasks?limit=50&offset=0")).statusCode).toBe(200);
  });
});

describe("PUT и DELETE", () => {
  it("PUT заменяет задачу целиком, повтор даёт тот же результат", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "old", done: true });
    const first = await call("PUT", "/tasks/1", { title: "new", done: false, id: 55 });
    expect(first.statusCode).toBe(200);
    expect(first.json()).toEqual({ id: 1, title: "new", done: false });
    const again = await call("PUT", "/tasks/1", { title: "new", done: false });
    expect([again.statusCode, again.json()]).toEqual([200, first.json()]);
    expect((await call("GET", "/tasks")).json().total).toBe(1);
    expect((await call("GET", "/tasks/1")).json()).toEqual({ id: 1, title: "new", done: false });
  });

  it("PUT с неполным телом - 400, PUT несуществующей задачи - 404 и ничего не создаёт", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "keep", done: true });
    for (const body of [{ title: "only title" }, { done: true }, { title: "", done: true }, { title: "x", done: 1 }]) {
      const res = await call("PUT", "/tasks/1", body);
      expect([res.statusCode, errorCode(res)]).toEqual([400, "validation_failed"]);
    }
    expect((await call("GET", "/tasks/1")).json()).toEqual({ id: 1, title: "keep", done: true });
    const missing = await call("PUT", "/tasks/7", { title: "ghost", done: false });
    expect([missing.statusCode, errorCode(missing)]).toEqual([404, "task_not_found"]);
    expect((await call("GET", "/tasks")).json().total).toBe(1);
  });

  it("DELETE - 204 без тела, повтор - 404, идентификаторы не переиспользуются", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "a" });
    await call("POST", "/tasks", { title: "b" });
    const removed = await call("DELETE", "/tasks/2");
    expect([removed.statusCode, removed.body]).toEqual([204, ""]);
    const again = await call("DELETE", "/tasks/2");
    expect([again.statusCode, errorCode(again)]).toEqual([404, "task_not_found"]);
    const created = await call("POST", "/tasks", { title: "c" });
    expect(created.json().id).toBe(3);
    expect((await call("GET", "/tasks")).json().items.map((t) => t.id)).toEqual([1, 3]);
  });
});

describe("Методы и ошибки", () => {
  it("неподдерживаемый метод - 405 с Allow", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "a" });
    const onCollection = await call("DELETE", "/tasks");
    expect([onCollection.statusCode, onCollection.headers.allow, errorCode(onCollection)]).toEqual([405, "GET, POST", "method_not_allowed"]);
    const patch = await call("PATCH", "/tasks/1", { title: "x" });
    expect([patch.statusCode, patch.headers.allow, errorCode(patch)]).toEqual([405, "GET, PUT, DELETE", "method_not_allowed"]);
    const post = await call("POST", "/tasks/1", { title: "x" });
    expect(post.statusCode).toBe(405);
  });

  it("неверный JSON - 400 invalid_json, неизвестный путь - 404, после ошибок приложение работает", async () => {
    const { app, call } = setup();
    const broken = await inject(app, { method: "POST", url: "/tasks", headers: { "content-type": "application/json" }, payload: "{oops" });
    expect([broken.statusCode, errorCode(broken)]).toEqual([400, "invalid_json"]);
    const unknown = await call("GET", "/nowhere");
    expect([unknown.statusCode, errorCode(unknown)]).toEqual([404, "not_found"]);
    expect((await call("POST", "/tasks", { title: "still works" })).statusCode).toBe(201);
  });

  it("два приложения не делят данные", async () => {
    const a = setup();
    const b = setup();
    await a.call("POST", "/tasks", { title: "only in a" });
    expect((await a.call("GET", "/tasks")).json().total).toBe(1);
    expect((await b.call("GET", "/tasks")).json().total).toBe(0);
    expect((await b.call("POST", "/tasks", { title: "first in b" })).json().id).toBe(1);
  });
});
