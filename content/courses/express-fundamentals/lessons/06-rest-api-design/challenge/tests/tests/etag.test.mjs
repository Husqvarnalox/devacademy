import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";
import { etagOf, ifMatch, ifNoneMatch } from "../src/etag.mjs";

function setup() {
  const app = createApp();
  const call = (method, url, body, headers = {}) =>
    inject(app, {
      method,
      url,
      headers: body === undefined ? headers : { "content-type": "application/json", ...headers },
      payload: body === undefined ? undefined : JSON.stringify(body),
    });
  return { call };
}

describe("Функции условных заголовков", () => {
  it("etagOf: строгий тег в кавычках, равные значения дают равные теги", () => {
    const tag = etagOf({ id: 1, title: "a", done: false });
    expect(tag).toMatch(/^"[0-9a-f]{16}"$/);
    expect(etagOf({ id: 1, title: "a", done: false })).toBe(tag);
    expect(etagOf({ id: 1, title: "b", done: false })).not.toBe(tag);
    expect(etagOf({ id: 1, title: "a", done: true })).not.toBe(tag);
    expect(etagOf([1, 2])).toBe(etagOf([1, 2]));
  });

  it("ifNoneMatch: *, список и слабое сравнение", () => {
    const tag = '"abc"';
    expect(ifNoneMatch(undefined, tag)).toBe(false);
    expect(ifNoneMatch("*", tag)).toBe(true);
    expect(ifNoneMatch('"abc"', tag)).toBe(true);
    expect(ifNoneMatch('W/"abc"', tag)).toBe(true);
    expect(ifNoneMatch('"x", "abc" ,"y"', tag)).toBe(true);
    expect(ifNoneMatch('"x","y"', tag)).toBe(false);
    expect(ifNoneMatch("", tag)).toBe(false);
    expect(ifNoneMatch('"abc"', 'W/"abc"')).toBe(true);
  });

  it("ifMatch: сильное сравнение, слабые теги не совпадают", () => {
    const tag = '"abc"';
    expect(ifMatch(undefined, tag)).toBe(true);
    expect(ifMatch("*", tag)).toBe(true);
    expect(ifMatch('"abc"', tag)).toBe(true);
    expect(ifMatch('"x", "abc"', tag)).toBe(true);
    expect(ifMatch('W/"abc"', tag)).toBe(false);
    expect(ifMatch('"other"', tag)).toBe(false);
    expect(ifMatch("", tag)).toBe(false);
  });
});

describe("Условный GET", () => {
  it("GET отдаёт ETag, повторный GET с If-None-Match - 304 без тела", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "a" });
    const first = await call("GET", "/tasks/1");
    expect(first.statusCode).toBe(200);
    expect(first.headers["content-type"]).toMatch(/application\/json/);
    expect(first.json()).toEqual({ id: 1, title: "a", done: false });
    const tag = first.headers.etag;
    expect(tag).toMatch(/^"[0-9a-f]{16}"$/);
    const second = await call("GET", "/tasks/1", undefined, { "if-none-match": tag });
    expect(second.statusCode).toBe(304);
    expect(second.body).toBe("");
    expect(second.headers.etag).toBe(tag);
  });

  it("другие формы If-None-Match: *, список, слабый тег, чужой тег", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "a" });
    const tag = (await call("GET", "/tasks/1")).headers.etag;
    const status = async (value) => (await call("GET", "/tasks/1", undefined, { "if-none-match": value })).statusCode;
    expect(await status("*")).toBe(304);
    expect(await status(`"zzz", ${tag}`)).toBe(304);
    expect(await status(`W/${tag}`)).toBe(304);
    expect(await status('"zzz"')).toBe(200);
  });

  it("после изменения задачи старый тег даёт 200 с новым ETag", async () => {
    const { call } = setup();
    await call("POST", "/tasks", { title: "a" });
    const oldTag = (await call("GET", "/tasks/1")).headers.etag;
    const put = await call("PUT", "/tasks/1", { title: "a", done: true });
    expect(put.statusCode).toBe(200);
    expect(put.headers.etag).not.toBe(oldTag);
    const res = await call("GET", "/tasks/1", undefined, { "if-none-match": oldTag });
    expect(res.statusCode).toBe(200);
    expect(res.headers.etag).toBe(put.headers.etag);
    expect(res.json().done).toBe(true);
    const same = await call("GET", "/tasks/1", undefined, { "if-none-match": put.headers.etag });
    expect(same.statusCode).toBe(304);
  });

  it("список задач тоже получает ETag, добавление задачи его меняет", async () => {
    const { call } = setup();
    const empty = await call("GET", "/tasks");
    expect(empty.headers.etag).toMatch(/^"[0-9a-f]{16}"$/);
    expect((await call("GET", "/tasks", undefined, { "if-none-match": empty.headers.etag })).statusCode).toBe(304);
    await call("POST", "/tasks", { title: "a" });
    const after = await call("GET", "/tasks", undefined, { "if-none-match": empty.headers.etag });
    expect(after.statusCode).toBe(200);
    expect(after.json().total).toBe(1);
    expect(after.headers.etag).not.toBe(empty.headers.etag);
  });

  it("ошибки не получают ETag, а условные заголовки их не меняют", async () => {
    const { call } = setup();
    const ok = await call("POST", "/tasks", { title: "a" });
    expect(ok.headers.etag).toMatch(/^"[0-9a-f]{16}"$/);
    const missing = await call("GET", "/tasks/9", undefined, { "if-none-match": "*" });
    expect(missing.statusCode).toBe(404);
    expect(missing.headers.etag).toBeUndefined();
    expect(missing.json().error.code).toBe("task_not_found");
  });
});

describe("Условный PUT", () => {
  it("If-Match: верный тег - 200, устаревший - 412, без заголовка - 200", async () => {
    const { call } = setup();
    const created = await call("POST", "/tasks", { title: "a" });
    const tag = created.headers.etag;
    const ok = await call("PUT", "/tasks/1", { title: "b", done: false }, { "if-match": tag });
    expect(ok.statusCode).toBe(200);
    expect(ok.json().title).toBe("b");
    const stale = await call("PUT", "/tasks/1", { title: "c", done: false }, { "if-match": tag });
    expect(stale.statusCode).toBe(412);
    expect(stale.json().error.code).toBe("precondition_failed");
    expect((await call("GET", "/tasks/1")).json().title).toBe("b");
    const noHeader = await call("PUT", "/tasks/1", { title: "d", done: false });
    expect(noHeader.statusCode).toBe(200);
    const weak = await call("PUT", "/tasks/1", { title: "e", done: false }, { "if-match": `W/${noHeader.headers.etag}` });
    expect(weak.statusCode).toBe(412);
    const star = await call("PUT", "/tasks/1", { title: "f", done: false }, { "if-match": "*" });
    expect(star.statusCode).toBe(200);
  });
});
