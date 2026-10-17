import { describe, it, expect } from "vitest";
import express from "express";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";
import { createBooksRouter } from "../src/routes/books.mjs";
import { createAuthorsRouter } from "../src/routes/authors.mjs";

// Фикстура: свежие данные для каждого теста.
const makeStore = () => ({
  authors: [
    { id: 1, name: "Ann" },
    { id: 2, name: "Bob" },
    { id: 3, name: "Cid" },
  ],
  books: [
    { id: 1, title: "Node", authorId: 1 },
    { id: 2, title: "Express", authorId: 1 },
    { id: 3, title: "HTTP", authorId: 2 },
  ],
});
const call = (app, url) => inject(app, { method: "GET", url });

describe("Приложение и роутеры", () => {
  it("индекс API", async () => {
    const res = await call(createApp({ store: makeStore() }), "/api");
    expect(res.statusCode).toBe(200);
    expect(res.json()).toEqual({ name: "library", resources: ["authors", "books"] });
  });

  it("список книг и заголовок роутера", async () => {
    const store = makeStore();
    const res = await call(createApp({ store }), "/api/books");
    expect(res.json()).toEqual({ items: store.books });
    expect(res.headers["x-resource"]).toBe("books");
  });

  it("книга по идентификатору", async () => {
    const res = await call(createApp({ store: makeStore() }), "/api/books/2");
    expect(res.statusCode).toBe(200);
    expect(res.json()).toEqual({ id: 2, title: "Express", authorId: 1 });
  });

  it("неизвестная книга - 404 из обработчика параметра", async () => {
    const app = createApp({ store: makeStore() });
    for (const id of ["99", "0", "abc", "1.5", "-1"]) {
      const res = await call(app, `/api/books/${id}`);
      expect(res.statusCode, id).toBe(404);
      expect(res.json(), id).toEqual({ error: "book not found" });
    }
    expect((await call(app, "/api/books/99/where")).statusCode).toBe(404);
  });
});

describe("Вложенность и монтирование", () => {
  it("книги автора через mergeParams", async () => {
    const app = createApp({ store: makeStore() });
    const res = await call(app, "/api/authors/1/books");
    expect(res.statusCode).toBe(200);
    expect(res.json()).toEqual({
      author: { id: 1, name: "Ann" },
      items: [
        { id: 1, title: "Node", authorId: 1 },
        { id: 2, title: "Express", authorId: 1 },
      ],
    });
    expect((await call(app, "/api/authors/3/books")).json().items).toEqual([]);
  });

  it("неизвестный автор - 404, в том числе для вложенного роутера", async () => {
    const app = createApp({ store: makeStore() });
    for (const url of ["/api/authors/9", "/api/authors/x", "/api/authors/9/books"]) {
      const res = await call(app, url);
      expect(res.statusCode, url).toBe(404);
      expect(res.json(), url).toEqual({ error: "author not found" });
    }
  });

  it("список авторов и автор по идентификатору", async () => {
    const store = makeStore();
    const app = createApp({ store });
    expect((await call(app, "/api/authors")).json()).toEqual({ items: store.authors });
    expect((await call(app, "/api/authors/2")).json()).toEqual({ id: 2, name: "Bob" });
  });

  it("заголовок X-Resource ставит только роутер книг", async () => {
    const app = createApp({ store: makeStore() });
    expect((await call(app, "/api/books/1")).headers["x-resource"]).toBe("books");
    expect((await call(app, "/api/authors")).headers["x-resource"]).toBeUndefined();
    expect((await call(app, "/api")).headers["x-resource"]).toBeUndefined();
  });

  it("baseUrl, path и originalUrl внутри смонтированного роутера", async () => {
    const res = await call(createApp({ store: makeStore() }), "/api/books/3/where?x=1");
    expect(res.json()).toEqual({ baseUrl: "/api/books", path: "/3/where", originalUrl: "/api/books/3/where?x=1" });
  });

  it("роутеры независимы: монтируются в другое приложение под другим префиксом и со своими данными", async () => {
    const store = makeStore();
    store.books.push({ id: 4, title: "New", authorId: 3 });
    const app = express();
    app.use("/v2/library/books", createBooksRouter(store));
    app.use("/v2/library/authors", createAuthorsRouter(store));
    const res = await call(app, "/v2/library/books/4/where");
    expect(res.json()).toEqual({ baseUrl: "/v2/library/books", path: "/4/where", originalUrl: "/v2/library/books/4/where" });
    expect((await call(app, "/v2/library/authors/3/books")).json().items).toEqual([{ id: 4, title: "New", authorId: 3 }]);
    // Данные первого приложения не затронуты.
    expect((await call(createApp({ store: makeStore() }), "/api/books/4")).statusCode).toBe(404);
  });

  it("неизвестные пути - JSON 404", async () => {
    const app = createApp({ store: makeStore() });
    for (const url of ["/", "/api/unknown", "/api/books/1/nope"]) {
      const res = await call(app, url);
      expect(res.statusCode, url).toBe(404);
      expect(res.json(), url).toEqual({ error: "not found" });
    }
  });
});
