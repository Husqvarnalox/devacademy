import express from "express";

const fail = (res, status, code, message) => res.status(status).json({ error: { code, message } });

// Ресурс "задача": { id: целое число, title: строка, done: boolean }.
// Все ошибки - { "error": { "code", "message" } }.
//
// POST /tasks
//   Тело { title, done? }. title - строка 1-100 символов, не пустая после trim; done - boolean, по умолчанию false.
//   Успех: 201, заголовок Location: /tasks/<id>, тело - созданная задача. Лишние поля тела игнорируются.
//   Ошибка тела: 400 validation_failed. id выдаются подряд с 1 и не переиспользуются.
// GET /tasks?done=true|false&q=текст&limit=&offset=
//   Ответ 200 { items, total, limit, offset }. По умолчанию limit=10, offset=0; limit - целое 1-50, offset - целое >= 0.
//   done - только "true" или "false"; q - подстрока заголовка без учёта регистра.
//   total - число подходящих задач до пагинации; порядок - по id. Неверные параметры: 400 invalid_query.
// GET /tasks/:id        200 задача | 404 task_not_found (в том числе для id вроде "abc" и "0").
// PUT /tasks/:id        Полная замена: обязательны title и done. 200 с задачей | 400 validation_failed | 404 task_not_found
//                       (PUT не создаёт задач). Повторный такой же PUT даёт тот же результат.
// DELETE /tasks/:id     204 без тела | 404 task_not_found (в том числе при повторе).
// Другие методы на /tasks и /tasks/:id: 405 method_not_allowed и заголовок Allow
//   (GET, POST для /tasks; GET, PUT, DELETE для /tasks/:id).
// Состояние принадлежит конкретному приложению: два createApp() не делят задачи.
export function createApp() {
  const app = express();
  app.use(express.json());

  // TODO: маршруты /tasks и /tasks/:id

  app.use((req, res) => fail(res, 404, "not_found", "Not found"));
  app.use((err, req, res, next) => {
    if (res.headersSent) return next(err);
    if (err.type === "entity.parse.failed") return fail(res, 400, "invalid_json", "Body is not valid JSON");
    fail(res, 500, "internal_error", "Internal Server Error");
  });
  return app;
}
