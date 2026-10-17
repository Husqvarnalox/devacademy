// Контракт ошибок приложения.
//
// Формат любого ответа об ошибке (Content-Type: application/json):
//   { "error": { "code": "<машинный код>", "message": "<текст для клиента>", "details": <необязательно> } }
// Ключ details присутствует только если подробности заданы.

// HttpError(status, code, message, details): наследник Error. Поля: name = "HttpError", status, code, details.
export class HttpError extends Error {}

// notFoundHandler: обычное middleware (req, res, next). Не отвечает само, а передаёт дальше
// HttpError(404, "not_found", `Cannot ${req.method} ${req.path}`).
export function notFoundHandler(req, res, next) {
  next();
}

// errorHandler({ log }): возвращает обработчик ошибок (err, req, res, next) - ровно четыре параметра.
//   * res.headersSent уже true: ничего не пишем, передаём ошибку дальше: next(err);
//   * HttpError: берём статус, код, сообщение и подробности из самой ошибки;
//   * ошибка express.json с err.type === "entity.parse.failed": 400, code "invalid_json", message "Request body is not valid JSON";
//   * err.type === "entity.too.large": 413, code "payload_too_large", message "Request body is too large";
//   * всё остальное (в том числе throw "строка" и не-Error): 500, code "internal_error", message "Internal Server Error".
//     Текст исходной ошибки клиенту не показываем.
//   * log(err) вызывается только для ответов со статусом 500 и выше (по одному разу).
export function errorHandler({ log = () => {} } = {}) {
  return (req, res, next) => {
    next();
  };
}
