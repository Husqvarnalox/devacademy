import express from "express";

const fail = (res, status, code, message) => res.status(status).json({ error: { code, message } });

// Возвращает текст ошибки или null. requireDone: PUT требует оба поля.
function checkTask(body, { requireDone }) {
  if (body === null || typeof body !== "object" || Array.isArray(body)) return "body must be a JSON object";
  if (typeof body.title !== "string" || body.title.trim() === "" || body.title.length > 100) {
    return "title must be a non-empty string of at most 100 characters";
  }
  if (body.done === undefined ? requireDone : typeof body.done !== "boolean") return "done must be a boolean";
  return null;
}

function parseListQuery(query) {
  const result = { done: undefined, q: undefined, limit: 10, offset: 0 };
  if (query.done !== undefined) {
    if (query.done !== "true" && query.done !== "false") return { message: "done must be true or false" };
    result.done = query.done === "true";
  }
  if (query.q !== undefined) {
    if (typeof query.q !== "string") return { message: "q must be a single string" };
    result.q = query.q.toLowerCase();
  }
  if (query.limit !== undefined) {
    if (typeof query.limit !== "string" || !/^\d+$/.test(query.limit) || Number(query.limit) < 1 || Number(query.limit) > 50) {
      return { message: "limit must be an integer from 1 to 50" };
    }
    result.limit = Number(query.limit);
  }
  if (query.offset !== undefined) {
    if (typeof query.offset !== "string" || !/^\d+$/.test(query.offset)) return { message: "offset must be a non-negative integer" };
    result.offset = Number(query.offset);
  }
  return { value: result };
}

export function createApp() {
  const tasks = new Map();
  let nextId = 1;

  const app = express();
  app.use(express.json());

  const methodNotAllowed = (allow) => (req, res) => {
    res.set("Allow", allow);
    fail(res, 405, "method_not_allowed", `Use one of: ${allow}`);
  };

  app.route("/tasks")
    .get((req, res) => {
      const parsed = parseListQuery(req.query);
      if (parsed.message) return fail(res, 400, "invalid_query", parsed.message);
      const { done, q, limit, offset } = parsed.value;
      const matching = [...tasks.values()].filter(
        (task) => (done === undefined || task.done === done) && (q === undefined || task.title.toLowerCase().includes(q)),
      );
      res.json({ items: matching.slice(offset, offset + limit), total: matching.length, limit, offset });
    })
    .post((req, res) => {
      const problem = checkTask(req.body, { requireDone: false });
      if (problem) return fail(res, 400, "validation_failed", problem);
      const task = { id: nextId, title: req.body.title, done: req.body.done ?? false };
      nextId += 1;
      tasks.set(task.id, task);
      res.status(201).location(`/tasks/${task.id}`).json(task);
    })
    .all(methodNotAllowed("GET, POST"));

  app.route("/tasks/:id")
    .all((req, res, next) => {
      const task = /^[1-9]\d*$/.test(req.params.id) ? tasks.get(Number(req.params.id)) : undefined;
      if (["GET", "PUT", "DELETE"].includes(req.method) && !task) {
        return fail(res, 404, "task_not_found", `Task ${req.params.id} does not exist`);
      }
      req.task = task;
      next();
    })
    .get((req, res) => res.json(req.task))
    .put((req, res) => {
      const problem = checkTask(req.body, { requireDone: true });
      if (problem) return fail(res, 400, "validation_failed", problem);
      const task = { id: req.task.id, title: req.body.title, done: req.body.done };
      tasks.set(task.id, task);
      res.json(task);
    })
    .delete((req, res) => {
      tasks.delete(req.task.id);
      res.status(204).end();
    })
    .all(methodNotAllowed("GET, PUT, DELETE"));

  app.use((req, res) => fail(res, 404, "not_found", "Not found"));
  app.use((err, req, res, next) => {
    if (res.headersSent) return next(err);
    if (err.type === "entity.parse.failed") return fail(res, 400, "invalid_json", "Body is not valid JSON");
    fail(res, 500, "internal_error", "Internal Server Error");
  });
  return app;
}
