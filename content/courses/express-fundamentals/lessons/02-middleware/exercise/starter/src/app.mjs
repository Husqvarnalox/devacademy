import express from "express";
import { requestId, accessLog, requireBearer } from "./middleware.mjs";

// createApp({ generateId, log, tokens }) собирает цепочку. Порядок регистрации важен:
//   1. requestId  - первым, чтобы идентификатор был у всех последующих шагов;
//   2. accessLog  - вторым, чтобы в журнал попали и отказы 401/403, и 404;
//   3. express.json() - разбор тела JSON;
//   4. маршруты: GET /health открыт всем; всё под /api защищено requireBearer на уровне роутера.
//
//   GET  /health   -> { status: "ok" }
//   GET  /api/me   -> { user: req.user, requestId: req.id }
//   POST /api/echo -> { echo: req.body ?? null }
export function createApp({ generateId, log, tokens }) {
  const app = express();

  // TODO: подключите middleware и маршруты в правильном порядке

  return app;
}
