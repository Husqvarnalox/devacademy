import express from "express";
import { requireApiKey, requireJson } from "./middleware.mjs";

const fail = (res, status, code, message) => res.status(status).json({ error: { code, message } });

function parseBookmark(body) {
  if (body === null || typeof body !== "object" || Array.isArray(body)) return { message: "body must be a JSON object" };
  let url;
  try {
    url = new URL(body.url);
  } catch {
    return { message: "url must be an absolute http(s) URL" };
  }
  if (url.protocol !== "http:" && url.protocol !== "https:") return { message: "url must use http or https" };
  if (body.title !== undefined && (typeof body.title !== "string" || body.title.length > 100)) {
    return { message: "title must be a string of at most 100 characters" };
  }
  return { value: { url: url.href, title: body.title ?? "" } };
}

export function createApp({ apiKey = "secret" } = {}) {
  const bookmarks = new Map();
  let nextId = 1;

  const app = express();
  app.use(express.json());
  app.use("/bookmarks", requireApiKey(apiKey));

  app.get("/bookmarks", (req, res) => {
    const items = [...bookmarks.values()];
    res.json({ items, total: items.length });
  });

  app.post("/bookmarks", requireJson, (req, res) => {
    const parsed = parseBookmark(req.body);
    if (parsed.message) return fail(res, 400, "validation_failed", parsed.message);
    const bookmark = { id: nextId, ...parsed.value };
    nextId += 1;
    bookmarks.set(bookmark.id, bookmark);
    res.status(201).location(`/bookmarks/${bookmark.id}`).json(bookmark);
  });

  app.get("/bookmarks/:id", (req, res) => {
    const bookmark = bookmarks.get(Number(req.params.id));
    if (!bookmark) return fail(res, 404, "bookmark_not_found", `Bookmark ${req.params.id} does not exist`);
    res.json(bookmark);
  });

  app.delete("/bookmarks/:id", (req, res) => {
    if (!bookmarks.delete(Number(req.params.id))) {
      return fail(res, 404, "bookmark_not_found", `Bookmark ${req.params.id} does not exist`);
    }
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
