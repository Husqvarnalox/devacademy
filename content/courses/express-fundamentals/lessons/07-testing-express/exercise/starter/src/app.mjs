import express from "express";
import { requireApiKey, requireJson } from "./middleware.mjs";

const fail = (res, status, code, message) => res.status(status).json({ error: { code, message } });

// Контракт API закладок (тесты проверяют именно его):
//   Все пути /bookmarks требуют заголовок X-Api-Key (по умолчанию "secret"), иначе 401 unauthorized.
//   POST /bookmarks  (Content-Type: application/json; параметр charset допустим, иначе 415 unsupported_media_type)
//       Тело { url, title? }: url - абсолютный адрес http или https, title - строка до 100 символов.
//       Адрес хранится в нормализованном виде (new URL(url).href), поэтому "https://example.com" превращается в "https://example.com/".
//       Успех: 201, Location: /bookmarks/<id>, тело { id, url, title } (title по умолчанию ""). Ошибка: 400 validation_failed.
//   GET /bookmarks        200 { items, total } в порядке создания.
//   GET /bookmarks/:id    200 закладка | 404 bookmark_not_found.
//   DELETE /bookmarks/:id 204 | 404 bookmark_not_found.
//   Неверный JSON: 400 invalid_json. Неизвестный путь: 404 not_found.
//   Два вызова createApp() не делят данные.

const bookmarks = [];
let nextId = 1;

function isValidUrl(value) {
  return typeof value === "string" && value.includes(":");
}

export function createApp({ apiKey = "secret" } = {}) {
  const app = express();
  app.use(express.json());
  app.use("/bookmarks", requireApiKey(apiKey));

  app.get("/bookmarks", (req, res) => {
    res.json({ items: bookmarks, total: bookmarks.length });
  });

  app.post("/bookmarks", requireJson, (req, res) => {
    if (!isValidUrl(req.body?.url)) return fail(res, 400, "validation_failed", "url is invalid");
    const bookmark = { id: nextId, url: req.body.url, title: req.body.title ?? "" };
    nextId += 1;
    bookmarks.push(bookmark);
    res.json(bookmark);
  });

  app.get("/bookmarks/:id", (req, res) => {
    const bookmark = bookmarks.find((b) => b.id === Number(req.params.id));
    res.json(bookmark ?? null);
  });

  app.delete("/bookmarks/:id", (req, res) => {
    const index = bookmarks.findIndex((b) => b.id === Number(req.params.id));
    if (index === -1) return fail(res, 404, "bookmark_not_found", `Bookmark ${req.params.id} does not exist`);
    bookmarks.splice(index, 1);
    res.status(204).end();
  });

  app.use((req, res) => fail(res, 404, "not_found", "Not found"));
  app.use((err, req, res, next) => {
    if (res.headersSent) return next(err);
    if (err.type === "entity.parse.failed") return fail(res, 400, "invalid_json", "Body is not valid JSON");
    fail(res, 500, "internal_error", "Internal Server Error");
  });
  return app;
}
