// Набор middleware "безопасные настройки по умолчанию".
// Каждая фабрика возвращает обычное middleware (req, res, next).

// securityHeaders() - на КАЖДЫЙ ответ (в том числе 404) ставит:
//   X-Content-Type-Options: nosniff
//   Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
//   Referrer-Policy: no-referrer
//   Strict-Transport-Security: max-age=15552000; includeSubDomains
//   Cross-Origin-Resource-Policy: same-origin
// и убирает X-Powered-By (res.removeHeader).
export function securityHeaders() {
  return (req, res, next) => {
    next();
  };
}

// cors({ allowedOrigins }):
//   - всегда res.vary("Origin");
//   - запрос без заголовка Origin - просто next();
//   - Origin из списка: Access-Control-Allow-Origin = ровно этот Origin (не "*");
//   - preflight (метод OPTIONS и заголовок Access-Control-Request-Method) от разрешённого Origin:
//     ответ 204 без тела с Access-Control-Allow-Methods: GET, POST, PUT, DELETE,
//     Access-Control-Allow-Headers: Content-Type, Authorization, Access-Control-Max-Age: 600;
//   - preflight от чужого Origin: 403 и { "error": { "code": "origin_not_allowed", "message": "Origin is not allowed" } };
//   - обычный запрос от чужого Origin: next() без Access-Control-Allow-Origin.
export function cors({ allowedOrigins = [] } = {}) {
  return (req, res, next) => {
    next();
  };
}

// rateLimit({ limit, windowMs, now, keyOf }) - фиксированное окно для каждого ключа.
//   - ключ: keyOf(req), по умолчанию req.ip; время: now() в миллисекундах;
//   - окно ключа начинается с его первого запроса и длится windowMs; когда now() >= конца окна, счётчик начинается заново;
//   - на каждый ответ: RateLimit-Limit = limit, RateLimit-Remaining = max(0, limit - число запросов в окне),
//     RateLimit-Reset = секунд до конца окна, округлённых вверх;
//   - запрос номер limit + 1 и далее: 429, заголовок Retry-After (те же секунды)
//     и { "error": { "code": "rate_limited", "message": "Too many requests" } };
//   - счётчики принадлежат конкретному вызову rateLimit(...), а не модулю.
export function rateLimit({ limit, windowMs, now = Date.now, keyOf = (req) => req.ip } = {}) {
  return (req, res, next) => {
    next();
  };
}

// setSessionCookie(res, value) - cookie "sid" с флагами HttpOnly, Secure, SameSite=Lax,
// Path=/ и Max-Age=3600 (res.cookie принимает maxAge в миллисекундах).
export function setSessionCookie(res, value) {}
