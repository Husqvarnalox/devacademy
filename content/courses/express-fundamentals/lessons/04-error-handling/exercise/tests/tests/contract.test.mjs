import { describe, it, expect, vi } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";
import { HttpError, errorHandler, notFoundHandler } from "../src/errors.mjs";

function setup() {
  const logged = [];
  const items = new Map([[1, { id: 1, name: "first" }]]);
  const app = createApp({ items, log: (err) => logged.push(err) });
  const call = (url, options = {}) => inject(app, { method: "GET", url, ...options });
  return { call, logged, items };
}

const noLeak = (res) => {
  expect(res.body).not.toContain("hunter2");
  expect(res.body).not.toContain("secret async failure");
  expect(res.body).not.toContain("just a string");
  expect(res.body).not.toContain("    at ");
};

describe("HttpError", () => {
  it("несёт статус, код и подробности и остаётся Error", () => {
    const err = new HttpError(409, "conflict", "Already exists", { id: 7 });
    expect(err).toBeInstanceOf(Error);
    expect(err.name).toBe("HttpError");
    expect(err.message).toBe("Already exists");
    expect([err.status, err.code, err.details]).toEqual([409, "conflict", { id: 7 }]);
    expect(new HttpError(404, "x", "y").details).toBeUndefined();
  });
});

describe("Единый формат ошибок", () => {
  it("неизвестный маршрут - 404 в общем формате", async () => {
    const { call } = setup();
    const res = await call("/nope");
    expect(res.statusCode).toBe(404);
    expect(res.headers["content-type"]).toMatch(/^application\/json/);
    expect(res.json()).toEqual({ error: { code: "not_found", message: "Cannot GET /nope" } });
    const post = await call("/items/1", { method: "DELETE" });
    expect(post.json()).toEqual({ error: { code: "not_found", message: "Cannot DELETE /items/1" } });
  });

  it("ошибка из async-обработчика попадает в общий формат без ручного try/catch", async () => {
    const { call, logged } = setup();
    const res = await call("/items/42");
    expect(res.statusCode).toBe(404);
    expect(res.json()).toEqual({ error: { code: "item_not_found", message: "Item 42 does not exist" } });
    expect(logged).toEqual([]);
    expect((await call("/items/1")).json()).toEqual({ id: 1, name: "first" });
  });

  it("неожиданная ошибка - 500 без утечки внутренностей", async () => {
    const { call } = setup();
    const res = await call("/crash/sync");
    expect(res.statusCode).toBe(500);
    expect(res.headers["content-type"]).toMatch(/^application\/json/);
    expect(res.json()).toEqual({ error: { code: "internal_error", message: "Internal Server Error" } });
    noLeak(res);
  });

  it("подробности есть в ответе только когда заданы", async () => {
    const { call } = setup();
    const bad = await call("/items/abc");
    expect(bad.statusCode).toBe(400);
    expect(bad.json()).toEqual({ error: { code: "invalid_id", message: "id must be a positive integer", details: { id: "abc" } } });
    const noDetails = await call("/items/9");
    expect("details" in noDetails.json().error).toBe(false);
  });
});

describe("Источники ошибок", () => {
  it("async-ошибка и throw не-Error дают такой же 500 и пишутся в журнал по одному разу", async () => {
    const { call, logged } = setup();
    const a = await call("/crash/async");
    const s = await call("/crash/string");
    for (const res of [a, s]) {
      expect(res.statusCode).toBe(500);
      expect(res.json()).toEqual({ error: { code: "internal_error", message: "Internal Server Error" } });
      noLeak(res);
    }
    expect(logged).toHaveLength(2);
    expect(logged[0]).toBeInstanceOf(Error);
    expect(logged[0].message).toBe("secret async failure");
    expect(logged[1]).toBe("just a string");
  });

  it("ошибки клиента (4xx) в журнал ошибок сервера не попадают", async () => {
    const { call, logged } = setup();
    await call("/nope");
    await call("/items/zzz");
    await call("/items", { method: "POST", payload: {} });
    expect(logged).toEqual([]);
  });

  it("неверный JSON - 400 invalid_json, слишком большое тело - 413", async () => {
    const { call, logged } = setup();
    const broken = await call("/items", { method: "POST", headers: { "content-type": "application/json" }, payload: "{bad json" });
    expect(broken.statusCode).toBe(400);
    expect(broken.json()).toEqual({ error: { code: "invalid_json", message: "Request body is not valid JSON" } });
    const big = await call("/items", { method: "POST", payload: { name: "x".repeat(3000) } });
    expect(big.statusCode).toBe(413);
    expect(big.json()).toEqual({ error: { code: "payload_too_large", message: "Request body is too large" } });
    expect(logged).toEqual([]);
  });

  it("проверка тела: 400 с подробностями, успех - 201", async () => {
    const { call, items } = setup();
    const bad = await call("/items", { method: "POST", payload: { name: "  " } });
    expect(bad.statusCode).toBe(400);
    expect(bad.json()).toEqual({ error: { code: "validation_failed", message: "name is required", details: { field: "name" } } });
    const ok = await call("/items", { method: "POST", payload: { name: " second " } });
    expect(ok.statusCode).toBe(201);
    expect(ok.json()).toEqual({ id: 2, name: "second" });
    expect(items.size).toBe(2);
  });
});

describe("Обработчики по отдельности", () => {
  it("errorHandler объявлен с четырьмя параметрами - иначе Express не считает его обработчиком ошибок", () => {
    expect(errorHandler().length).toBe(4);
    expect(errorHandler({ log() {} }).length).toBe(4);
  });

  it("если заголовки уже отправлены, ошибка передаётся дальше и ответ не трогается", () => {
    const err = new Error("late");
    const next = vi.fn();
    const res = { headersSent: true, status: vi.fn(), json: vi.fn() };
    errorHandler({ log: vi.fn() })(err, {}, res, next);
    expect(next).toHaveBeenCalledOnce();
    expect(next).toHaveBeenCalledWith(err);
    expect(res.status).not.toHaveBeenCalled();
    expect(res.json).not.toHaveBeenCalled();
  });

  it("notFoundHandler только передаёт HttpError дальше", () => {
    const next = vi.fn();
    const res = { status: vi.fn(), json: vi.fn() };
    notFoundHandler({ method: "PUT", path: "/x" }, res, next);
    expect(res.json).not.toHaveBeenCalled();
    const err = next.mock.calls[0][0];
    expect(err).toBeInstanceOf(HttpError);
    expect([err.status, err.code, err.message]).toEqual([404, "not_found", "Cannot PUT /x"]);
  });
});
