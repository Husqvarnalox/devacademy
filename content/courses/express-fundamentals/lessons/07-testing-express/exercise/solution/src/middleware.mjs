const fail = (res, status, code, message) => res.status(status).json({ error: { code, message } });

// Требует заголовок X-Api-Key с ожидаемым ключом.
export function requireApiKey(apiKey) {
  return (req, res, next) => {
    if (req.get("x-api-key") !== apiKey) {
      return fail(res, 401, "unauthorized", "Missing or invalid API key");
    }
    next();
  };
}

// Пропускает только запросы с телом application/json (параметры вроде charset допустимы).
export function requireJson(req, res, next) {
  if (!req.is("application/json")) {
    return fail(res, 415, "unsupported_media_type", "Content-Type must be application/json");
  }
  next();
}
