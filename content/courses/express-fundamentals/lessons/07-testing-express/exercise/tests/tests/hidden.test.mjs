import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";

function setup() {
  const app = createApp({ apiKey: "k1" });
  const call = (method, url, { body, headers = {}, raw } = {}) =>
    inject(app, {
      method,
      url,
      headers: { "x-api-key": "k1", ...(body !== undefined || raw !== undefined ? { "content-type": "application/json" } : {}), ...headers },
      payload: raw ?? (body === undefined ? undefined : JSON.stringify(body)),
    });
  return { call };
}

describe("Проверка запросов", () => {
  it("Content-Type с charset принимается, text/plain - 415", async () => {
    const { call } = setup();
    const withCharset = await call("POST", "/bookmarks", {
      body: { url: "https://example.com" },
      headers: { "content-type": "application/json; charset=utf-8" },
    });
    expect(withCharset.statusCode).toBe(201);
    const plain = await call("POST", "/bookmarks", { raw: '{"url":"https://example.com"}', headers: { "content-type": "text/plain" } });
    expect(plain.statusCode).toBe(415);
    expect(plain.json().error.code).toBe("unsupported_media_type");
  });

  it("небезопасные и некорректные адреса отклоняются, http и https принимаются", async () => {
    const { call } = setup();
    for (const url of ["javascript:alert(1)", "ftp://example.com/file", "data:text/html,hi", "not a url", "httpfoo", "", 5, null]) {
      const res = await call("POST", "/bookmarks", { body: { url } });
      expect([String(url), res.statusCode]).toEqual([String(url), 400]);
      expect(res.json().error.code).toBe("validation_failed");
    }
    expect((await call("POST", "/bookmarks", { body: { url: "http://a.example" } })).statusCode).toBe(201);
    expect((await call("POST", "/bookmarks", { body: { url: "https://b.example/path?q=1" } })).statusCode).toBe(201);
    expect((await call("POST", "/bookmarks", { body: { url: "https://c.example", title: "x".repeat(101) } })).statusCode).toBe(400);
    expect((await call("GET", "/bookmarks")).json().total).toBe(2);
  });

  it("неверный JSON - 400 invalid_json, и приложение продолжает работать", async () => {
    const { call } = setup();
    const broken = await call("POST", "/bookmarks", { raw: "{oops" });
    expect([broken.statusCode, broken.json().error.code]).toEqual([400, "invalid_json"]);
    expect((await call("POST", "/bookmarks", { body: { url: "https://example.com" } })).statusCode).toBe(201);
  });
});

describe("Данные и жизненный цикл", () => {
  it("два приложения не делят закладки", async () => {
    const a = setup();
    const b = setup();
    expect((await a.call("POST", "/bookmarks", { body: { url: "https://a.example" } })).statusCode).toBe(201);
    expect((await a.call("GET", "/bookmarks")).json().total).toBe(1);
    expect((await b.call("GET", "/bookmarks")).json()).toEqual({ items: [], total: 0 });
    const first = await b.call("POST", "/bookmarks", { body: { url: "https://b.example" } });
    expect(first.json().id).toBe(1);
  });

  it("список в порядке создания, DELETE - 204, повтор - 404", async () => {
    const { call } = setup();
    for (const host of ["a", "b", "c"]) {
      expect((await call("POST", "/bookmarks", { body: { url: `https://${host}.example` } })).statusCode).toBe(201);
    }
    const list = (await call("GET", "/bookmarks")).json();
    expect(list.items.map((b) => b.id)).toEqual([1, 2, 3]);
    expect(list.total).toBe(3);
    const removed = await call("DELETE", "/bookmarks/2");
    expect([removed.statusCode, removed.body]).toEqual([204, ""]);
    const again = await call("DELETE", "/bookmarks/2");
    expect([again.statusCode, again.json().error.code]).toEqual([404, "bookmark_not_found"]);
    expect((await call("GET", "/bookmarks")).json().items.map((b) => b.id)).toEqual([1, 3]);
    expect((await call("GET", "/bookmarks/2")).statusCode).toBe(404);
  });

  it("title по умолчанию пустой, адрес нормализуется, неизвестный путь - 404", async () => {
    const { call } = setup();
    const res = await call("POST", "/bookmarks", { body: { url: "https://example.com" } });
    expect(res.statusCode).toBe(201);
    expect(res.json()).toEqual({ id: 1, url: "https://example.com/", title: "" });
    const fetched = await call("GET", "/bookmarks/1");
    expect(fetched.json()).toEqual(res.json());
    const missing = await call("GET", "/nowhere");
    expect([missing.statusCode, missing.json().error.code]).toEqual([404, "not_found"]);
  });
});
