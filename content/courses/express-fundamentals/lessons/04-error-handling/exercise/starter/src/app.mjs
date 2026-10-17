import express from "express";
import { HttpError, notFoundHandler, errorHandler } from "./errors.mjs";

export function createApp({ items = new Map(), log = () => {} } = {}) {
  const app = express();
  app.use(express.json({ limit: "1kb" }));

  app.get("/items/:id", async (req, res) => {
    if (!/^[1-9]\d*$/.test(req.params.id)) {
      throw new HttpError(400, "invalid_id", "id must be a positive integer", { id: req.params.id });
    }
    const item = await Promise.resolve(items.get(Number(req.params.id)));
    if (!item) throw new HttpError(404, "item_not_found", `Item ${req.params.id} does not exist`);
    res.json(item);
  });

  app.post("/items", (req, res) => {
    const name = req.body?.name;
    if (typeof name !== "string" || name.trim() === "") {
      throw new HttpError(400, "validation_failed", "name is required", { field: "name" });
    }
    const item = { id: items.size + 1, name: name.trim() };
    items.set(item.id, item);
    res.status(201).json(item);
  });

  app.get("/crash/sync", () => {
    throw new Error("db password is hunter2");
  });
  app.get("/crash/async", async () => {
    await null;
    throw new Error("secret async failure");
  });
  app.get("/crash/string", () => {
    throw "just a string";
  });

  // TODO: подключите notFoundHandler и errorHandler({ log }) - они должны стоять после всех маршрутов

  return app;
}
