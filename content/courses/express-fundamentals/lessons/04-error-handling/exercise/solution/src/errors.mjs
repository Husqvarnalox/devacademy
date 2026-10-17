// Ошибка приложения: статус HTTP, машинный код, сообщение для клиента и необязательные подробности.
export class HttpError extends Error {
  constructor(status, code, message, details) {
    super(message);
    this.name = "HttpError";
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

// Не нашли маршрут: превращаем это в обычную ошибку, чтобы ответ формировал единый обработчик.
export function notFoundHandler(req, res, next) {
  next(new HttpError(404, "not_found", `Cannot ${req.method} ${req.path}`));
}

// Единый обработчик ошибок. Именно четыре параметра делают функцию обработчиком ошибок.
export function errorHandler({ log = () => {} } = {}) {
  return (err, req, res, next) => {
    // Часть ответа уже ушла: менять статус поздно, пусть решает встроенный обработчик (он закроет соединение).
    if (res.headersSent) return next(err);

    let status = 500;
    let code = "internal_error";
    let message = "Internal Server Error";
    let details;

    if (err instanceof HttpError) {
      ({ status, code, message, details } = err);
    } else if (err?.type === "entity.parse.failed") {
      status = 400;
      code = "invalid_json";
      message = "Request body is not valid JSON";
    } else if (err?.type === "entity.too.large") {
      status = 413;
      code = "payload_too_large";
      message = "Request body is too large";
    }

    if (status >= 500) log(err);

    const error = { code, message };
    if (details !== undefined) error.details = details;
    res.status(status).json({ error });
  };
}
