import express from "express";
import { etagOf, ifMatch, sendConditional } from "./etag.mjs";

const fail = (res, status, code, message) => res.status(status).json({ error: { code, message } });

export function createApp() {
  const tasks = new Map();
  let nextId = 1;

  const app = express();
  app.set("etag", false); // автоматические слабые ETag Express отключены: теги ставим сами
  app.use(express.json());

  app.get("/tasks", (req, res) => sendConditional(req, res, { items: [...tasks.values()], total: tasks.size }));

  app.post("/tasks", (req, res) => {
    if (typeof req.body?.title !== "string" || req.body.title.trim() === "") {
      return fail(res, 400, "validation_failed", "title is required");
    }
    const task = { id: nextId, title: req.body.title, done: Boolean(req.body.done) };
    nextId += 1;
    tasks.set(task.id, task);
    res.set("ETag", etagOf(task));
    res.status(201).location(`/tasks/${task.id}`).json(task);
  });

  app.use("/tasks/:id", (req, res, next) => {
    const task = tasks.get(Number(req.params.id));
    if (!task) return fail(res, 404, "task_not_found", "No such task");
    req.task = task;
    next();
  });
  app.get("/tasks/:id", (req, res) => sendConditional(req, res, req.task));
  app.put("/tasks/:id", (req, res) => {
    if (!ifMatch(req.get("If-Match"), etagOf(req.task))) {
      return fail(res, 412, "precondition_failed", "The task was changed by someone else");
    }
    if (typeof req.body?.title !== "string" || req.body.title.trim() === "" || typeof req.body.done !== "boolean") {
      return fail(res, 400, "validation_failed", "title and done are required");
    }
    const task = { id: req.task.id, title: req.body.title, done: req.body.done };
    tasks.set(task.id, task);
    res.set("ETag", etagOf(task));
    res.json(task);
  });

  app.use((req, res) => fail(res, 404, "not_found", "Not found"));
  return app;
}
