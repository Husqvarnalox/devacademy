import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";

// Фикстура: свежее приложение с детерминированными идентификаторами и журналом в массиве.
function setup() {
  let n = 0;
  const lines = [];
  const tokens = new Map([["t-ann", "ann"], ["t-bob", "bob"]]);
  const app = createApp({ generateId: () => `req-${++n}`, log: (line) => lines.push(line), tokens });
  const call = (url, options = {}) => inject(app, { method: "GET", url, ...options });
  return { call, lines };
}

const auth = (token) => ({ authorization: `Bearer ${token}` });

describe("requestId", () => {
  it("выдаёт последовательные идентификаторы и возвращает их в заголовке", async () => {
    const { call } = setup();
    expect((await call("/health")).headers["x-request-id"]).toBe("req-1");
    expect((await call("/health")).headers["x-request-id"]).toBe("req-2");
  });

  it("принимает допустимый клиентский идентификатор и не тратит генератор", async () => {
    const { call } = setup();
    const own = await call("/health", { headers: { "x-request-id": "client-abc_123" } });
    expect(own.headers["x-request-id"]).toBe("client-abc_123");
    expect((await call("/health")).headers["x-request-id"]).toBe("req-1");
  });

  it("недопустимый клиентский идентификатор заменяется сгенерированным", async () => {
    const { call } = setup();
    for (const bad of ["bad id!", "x".repeat(65), "<script>"]) {
      const res = await call("/health", { headers: { "x-request-id": bad } });
      expect(res.headers["x-request-id"], bad).toMatch(/^req-\d+$/);
    }
  });
});

describe("accessLog", () => {
  it("пишет строку после ответа", async () => {
    const { call, lines } = setup();
    await call("/health");
    expect(lines).toEqual(["req-1 GET /health 200"]);
  });

  it("журнал содержит исходный адрес с query и статусы отказов", async () => {
    const { call, lines } = setup();
    await call("/api/me?x=1", { headers: auth("t-ann") });
    await call("/api/me");
    await call("/api/me", { headers: auth("zzz") });
    await call("/nope");
    expect(lines).toEqual([
      "req-1 GET /api/me?x=1 200",
      "req-2 GET /api/me 401",
      "req-3 GET /api/me 403",
      "req-4 GET /nope 404",
    ]);
  });
});

describe("requireBearer", () => {
  it("без заголовка - 401 и WWW-Authenticate", async () => {
    const { call } = setup();
    const res = await call("/api/me");
    expect(res.statusCode).toBe(401);
    expect(res.headers["www-authenticate"]).toBe("Bearer");
    expect(res.json()).toEqual({ error: "unauthorized" });
  });

  it("неизвестный токен - 403, верный - 200", async () => {
    const { call } = setup();
    const bad = await call("/api/me", { headers: auth("nope") });
    expect(bad.statusCode).toBe(403);
    expect(bad.json()).toEqual({ error: "forbidden" });
    const ok = await call("/api/me", { headers: auth("t-bob") });
    expect(ok.statusCode).toBe(200);
    expect(ok.json()).toEqual({ user: "bob", requestId: "req-2" });
  });

  it("другая схема или пустой токен - 401; слово Bearer без учёта регистра", async () => {
    const { call } = setup();
    expect((await call("/api/me", { headers: { authorization: "Basic dDpw" } })).statusCode).toBe(401);
    expect((await call("/api/me", { headers: { authorization: "Bearer" } })).statusCode).toBe(401);
    expect((await call("/api/me", { headers: { authorization: "Bearer a b" } })).statusCode).toBe(401);
    expect((await call("/api/me", { headers: { authorization: "bearer t-ann" } })).statusCode).toBe(200);
  });
});

describe("Порядок и область действия", () => {
  it("защита роутера не затрагивает /health, но закрывает весь /api", async () => {
    const { call } = setup();
    expect((await call("/health")).statusCode).toBe(200);
    expect((await call("/api/unknown")).statusCode).toBe(401);
    expect((await call("/api/unknown", { headers: auth("t-ann") })).statusCode).toBe(404);
    expect((await call("/unknown")).statusCode).toBe(404);
  });

  it("express.json разбирает тело; без тела req.body не определён", async () => {
    const { call } = setup();
    const withBody = await call("/api/echo", { method: "POST", headers: auth("t-ann"), payload: { a: 1, list: [1, 2] } });
    expect(withBody.json()).toEqual({ echo: { a: 1, list: [1, 2] } });
    const without = await call("/api/echo", { method: "POST", headers: auth("t-ann") });
    expect(without.json()).toEqual({ echo: null });
  });

  it("идентификатор есть уже у отказа 401 (requestId стоит раньше accessLog)", async () => {
    const { call, lines } = setup();
    const res = await call("/api/echo", { method: "POST" });
    expect(res.statusCode).toBe(401);
    expect(res.headers["x-request-id"]).toBe("req-1");
    expect(lines).toEqual(["req-1 POST /api/echo 401"]);
  });
});
