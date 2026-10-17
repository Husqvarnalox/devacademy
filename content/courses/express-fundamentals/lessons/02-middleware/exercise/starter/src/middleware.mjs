// Три middleware для цепочки запроса. Каждая функция возвращает (req, res, next).

// requestId({ generate }): кладёт в req.id идентификатор запроса и возвращает его в заголовке ответа X-Request-Id.
// Если клиент прислал X-Request-Id, подходящий под /^[A-Za-z0-9_-]{1,64}$/, берётся он; иначе вызывается generate().
// generate() нельзя вызывать, когда принят клиентский идентификатор.
export function requestId({ generate }) {
  return (req, res, next) => {
    next();
  };
}

// accessLog(sink): когда ответ отправлен (событие "finish" у res), вызывает sink со строкой
// "<req.id> <метод> <req.originalUrl> <статус>", например "req-1 GET /api/me?x=1 200".
export function accessLog(sink) {
  return (req, res, next) => {
    next();
  };
}

// requireBearer({ tokens }): tokens - Map "токен -> имя пользователя".
// Нет заголовка Authorization или он не вида "Bearer <токен>" (слово Bearer без учёта регистра):
//   401, заголовок WWW-Authenticate: Bearer, тело { error: "unauthorized" }.
// Токена нет в tokens: 403 и { error: "forbidden" }.
// Иначе req.user = имя пользователя и next().
export function requireBearer({ tokens }) {
  return (req, res, next) => {
    next();
  };
}
