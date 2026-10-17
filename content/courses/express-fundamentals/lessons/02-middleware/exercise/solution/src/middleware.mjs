const ID_PATTERN = /^[A-Za-z0-9_-]{1,64}$/;

// Присваивает запросу идентификатор: берёт допустимый из заголовка X-Request-Id, иначе вызывает generate().
export function requestId({ generate }) {
  return (req, res, next) => {
    const incoming = req.get("X-Request-Id");
    req.id = incoming && ID_PATTERN.test(incoming) ? incoming : generate();
    res.set("X-Request-Id", req.id);
    next();
  };
}

// Пишет строку журнала после того, как ответ отправлен.
export function accessLog(sink) {
  return (req, res, next) => {
    res.on("finish", () => {
      sink(`${req.id} ${req.method} ${req.originalUrl} ${res.statusCode}`);
    });
    next();
  };
}

// Пропускает только запросы с заголовком "Authorization: Bearer <токен>" и известным токеном.
export function requireBearer({ tokens }) {
  return (req, res, next) => {
    const match = /^Bearer\s+(\S+)$/i.exec(req.get("Authorization") ?? "");
    if (!match) {
      res.set("WWW-Authenticate", "Bearer");
      return res.status(401).json({ error: "unauthorized" });
    }
    const user = tokens.get(match[1]);
    if (user === undefined) {
      return res.status(403).json({ error: "forbidden" });
    }
    req.user = user;
    next();
  };
}
