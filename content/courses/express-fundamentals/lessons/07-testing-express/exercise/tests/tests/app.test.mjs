import { describe, it, expect, vi } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";
import { requireApiKey } from "../src/middleware.mjs";

// Фикстура: каждый тест получает своё приложение и маленький помощник для запросов.
function setup() {
  const app = createApp({ apiKey: "k1" });
  const call = (method, url, { body, headers = {}, key = "k1" } = {}) =>
    inject(app, {
      method,
      url,
      headers: {
        ...(key === null ? {} : { "x-api-key": key }),
        ...(body === undefined ? {} : { "content-type": "application/json" }),
        ...headers,
      },
      payload: body === undefined ? undefined : JSON.stringify(body),
    });
  return { app, call };
}

describe("API закладок", () => {
  it("создание закладки: 201, Location и тело", async () => {
    const { call } = setup();
    const res = await call("POST", "/bookmarks", { body: { url: "https://example.com/a", title: "Пример" } });
    expect(res.statusCode).toBe(201);
    expect(res.headers.location).toBe("/bookmarks/1");
    expect(res.json()).toEqual({ id: 1, url: "https://example.com/a", title: "Пример" });
  });

  it("запрос без ключа отклоняется 401, и обработчик не выполняется", async () => {
    const { call } = setup();
    const denied = await call("POST", "/bookmarks", { key: null, body: { url: "https://example.com" } });
    expect(denied.statusCode).toBe(401);
    expect(denied.json().error.code).toBe("unauthorized");
    const wrong = await call("GET", "/bookmarks", { key: "nope" });
    expect(wrong.statusCode).toBe(401);
    expect((await call("GET", "/bookmarks")).json()).toEqual({ items: [], total: 0 });
  });

  it("неизвестная закладка - 404", async () => {
    const { call } = setup();
    const res = await call("GET", "/bookmarks/42");
    expect(res.statusCode).toBe(404);
    expect(res.json().error.code).toBe("bookmark_not_found");
  });
});

describe("requireApiKey по отдельности", () => {
  const makeReq = (key) => ({
    headers: key === undefined ? {} : { "x-api-key": key },
    get(name) { return this.headers[name.toLowerCase()]; },
  });
  const makeRes = () => ({ status: vi.fn().mockReturnThis(), json: vi.fn().mockReturnThis() });

  it("верный ключ вызывает next, неверный - отвечает 401 и next не вызывает", () => {
    const middleware = requireApiKey("k1");

    const okRes = makeRes();
    const okNext = vi.fn();
    middleware(makeReq("k1"), okRes, okNext);
    expect(okNext).toHaveBeenCalledTimes(1);
    expect(okRes.status).not.toHaveBeenCalled();

    const badRes = makeRes();
    const badNext = vi.fn();
    middleware(makeReq("wrong"), badRes, badNext);
    expect(badRes.status).toHaveBeenCalledWith(401);
    expect(badNext).not.toHaveBeenCalled();
  });
});
