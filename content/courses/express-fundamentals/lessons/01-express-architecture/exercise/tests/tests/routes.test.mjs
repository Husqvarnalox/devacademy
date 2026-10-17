import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";

const get = (url, method = "GET") => inject(createApp(), { method, url });

describe("Маршруты: параметры пути", () => {
  it("hello: приветствие по умолчанию", async () => {
    const res = await get("/hello/Ann");
    expect(res.statusCode).toBe(200);
    expect(res.headers["content-type"]).toMatch(/^application\/json/);
    expect(res.json()).toEqual({ greeting: "Hello, Ann!" });
  });

  it("hello: язык из строки запроса", async () => {
    expect((await get("/hello/Ann?lang=ru")).json()).toEqual({ greeting: "Привет, Ann!" });
    expect((await get("/hello/Ann?lang=es")).json()).toEqual({ greeting: "Hola, Ann!" });
  });

  it("hello: имя декодируется из процентной записи", async () => {
    const res = await get("/hello/%D0%90%D0%BD%D0%BD%D0%B0");
    expect(res.json()).toEqual({ greeting: "Hello, Анна!" });
  });

  it("hello: неизвестный язык - 400", async () => {
    for (const q of ["lang=de", "lang=constructor", "lang=ru&lang=en", "lang="]) {
      const res = await get(`/hello/Ann?${q}`);
      expect(res.statusCode, q).toBe(400);
      expect(res.json(), q).toEqual({ error: "unsupported lang" });
    }
  });
});

describe("Маршруты: query и сумма", () => {
  it("sum: складывает числа", async () => {
    const res = await get("/sum?a=2&b=40.5");
    expect(res.statusCode).toBe(200);
    expect(res.json()).toEqual({ sum: 42.5 });
    expect((await get("/sum?a=-1&b=1")).json()).toEqual({ sum: 0 });
  });

  it("sum: плохие параметры - 400", async () => {
    for (const q of ["", "a=1", "b=1", "a=&b=1", "a=x&b=1", "a=1&a=2&b=3", "a=Infinity&b=1"]) {
      const res = await get(`/sum?${q}`);
      expect(res.statusCode, q).toBe(400);
      expect(res.json(), q).toEqual({ error: "a and b must be numbers" });
    }
  });

  it("tags: повторяющийся параметр становится массивом", async () => {
    expect((await get("/tags")).json()).toEqual({ tags: [] });
    expect((await get("/tags?tag=a")).json()).toEqual({ tags: ["a"] });
    expect((await get("/tags?tag=a&tag=b&tag=c")).json()).toEqual({ tags: ["a", "b", "c"] });
  });
});

describe("Маршруты: синтаксис путей Express 5", () => {
  it("files: необязательное расширение в фигурных скобках", async () => {
    expect((await get("/files/report.pdf")).json()).toEqual({ name: "report", ext: "pdf" });
    expect((await get("/files/readme")).json()).toEqual({ name: "readme", ext: null });
    expect((await get("/files/a.tar.gz")).json()).toEqual({ name: "a.tar", ext: "gz" });
  });

  it("static: именованная подстановка даёт массив сегментов", async () => {
    expect((await get("/static/css/site/app.css")).json()).toEqual({ segments: ["css", "site", "app.css"] });
    expect((await get("/static/x")).json()).toEqual({ segments: ["x"] });
  });
});

describe("Маршруты: 404", () => {
  it("неизвестный путь - JSON с 404", async () => {
    const res = await get("/nope?x=1");
    expect(res.statusCode).toBe(404);
    expect(res.json()).toEqual({ error: "not found", path: "/nope" });
  });

  it("неподходящий метод и путь без подстановки - тоже 404, а не 405", async () => {
    const post = await get("/hello/Ann", "POST");
    expect(post.statusCode).toBe(404);
    expect(post.json()).toEqual({ error: "not found", path: "/hello/Ann" });
    expect((await get("/static")).statusCode).toBe(404);
  });
});
