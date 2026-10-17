// Набор middleware "безопасные настройки по умолчанию".
// Каждая фабрика возвращает обычное middleware (req, res, next).

const SECURITY_HEADERS = {
  "X-Content-Type-Options": "nosniff",
  "Content-Security-Policy": "default-src 'none'; frame-ancestors 'none'",
  "Referrer-Policy": "no-referrer",
  "Strict-Transport-Security": "max-age=15552000; includeSubDomains",
  "Cross-Origin-Resource-Policy": "same-origin",
};

export function securityHeaders() {
  return (req, res, next) => {
    for (const [name, value] of Object.entries(SECURITY_HEADERS)) res.set(name, value);
    res.removeHeader("X-Powered-By");
    next();
  };
}

export function cors({ allowedOrigins = [] } = {}) {
  const allowed = new Set(allowedOrigins);
  return (req, res, next) => {
    res.vary("Origin");
    const origin = req.get("Origin");
    if (origin === undefined) return next();
    const isPreflight = req.method === "OPTIONS" && req.get("Access-Control-Request-Method") !== undefined;
    if (!allowed.has(origin)) {
      if (!isPreflight) return next();
      return res.status(403).json({ error: { code: "origin_not_allowed", message: "Origin is not allowed" } });
    }
    res.set("Access-Control-Allow-Origin", origin);
    if (!isPreflight) return next();
    res.set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
    res.set("Access-Control-Allow-Headers", "Content-Type, Authorization");
    res.set("Access-Control-Max-Age", "600");
    res.status(204).end();
  };
}

export function rateLimit({ limit, windowMs, now = Date.now, keyOf = (req) => req.ip } = {}) {
  const buckets = new Map();
  return (req, res, next) => {
    const t = now();
    const key = keyOf(req);
    let bucket = buckets.get(key);
    if (!bucket || t >= bucket.resetAt) {
      bucket = { count: 0, resetAt: t + windowMs };
      buckets.set(key, bucket);
    }
    bucket.count += 1;
    const resetSeconds = Math.ceil((bucket.resetAt - t) / 1000);
    res.set("RateLimit-Limit", String(limit));
    res.set("RateLimit-Remaining", String(Math.max(0, limit - bucket.count)));
    res.set("RateLimit-Reset", String(resetSeconds));
    if (bucket.count > limit) {
      res.set("Retry-After", String(resetSeconds));
      return res.status(429).json({ error: { code: "rate_limited", message: "Too many requests" } });
    }
    next();
  };
}

export function setSessionCookie(res, value) {
  res.cookie("sid", value, { httpOnly: true, secure: true, sameSite: "lax", path: "/", maxAge: 3600 * 1000 });
}
