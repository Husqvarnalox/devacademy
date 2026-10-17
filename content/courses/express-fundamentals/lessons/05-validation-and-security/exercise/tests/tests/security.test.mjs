import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";
import { rateLimit } from "../src/security.mjs";

const GOOD = "https://app.example.com";

function setup(options = {}) {
  const clock = { t: 1_000_000 };
  const app = createApp({ allowedOrigins: [GOOD], limit: 3, windowMs: 10_000, now: () => clock.t, ...options });
  const call = (url, { method = "GET", headers = {}, ip = "10.0.0.1" } = {}) =>
    inject(app, { method, url, headers, remoteAddress: ip });
  return { call, clock, app };
}

describe("Заголовки безопасности", () => {
  it("обычный ответ получает защитные заголовки и теряет X-Powered-By", async () => {
    const res = await setup().call("/");
    expect(res.statusCode).toBe(200);
    expect(res.headers["x-content-type-options"]).toBe("nosniff");
    expect(res.headers["content-security-policy"]).toBe("default-src 'none'; frame-ancestors 'none'");
    expect(res.headers["referrer-policy"]).toBe("no-referrer");
    expect(res.headers["strict-transport-security"]).toBe("max-age=15552000; includeSubDomains");
    expect(res.headers["cross-origin-resource-policy"]).toBe("same-origin");
    expect(res.headers["x-powered-by"]).toBeUndefined();
  });

  it("заголовки есть и у ответа 404", async () => {
    const res = await setup().call("/nowhere");
    expect(res.statusCode).toBe(404);
    expect(res.headers["x-content-type-options"]).toBe("nosniff");
    expect(res.headers["x-powered-by"]).toBeUndefined();
  });
});

describe("CORS", () => {
  it("разрешённый Origin получает точное значение и Vary: Origin", async () => {
    const res = await setup().call("/", { headers: { origin: GOOD } });
    expect(res.headers["access-control-allow-origin"]).toBe(GOOD);
    expect(res.headers.vary).toMatch(/Origin/i);
  });

  it("чужой Origin не получает Access-Control-Allow-Origin, но Vary остаётся", async () => {
    const res = await setup().call("/", { headers: { origin: "https://evil.example" } });
    expect(res.statusCode).toBe(200);
    expect(res.headers["access-control-allow-origin"]).toBeUndefined();
    expect(res.headers.vary).toMatch(/Origin/i);
  });

  it("запрос без Origin не получает CORS-заголовков", async () => {
    const res = await setup().call("/");
    expect(res.headers["access-control-allow-origin"]).toBeUndefined();
    expect(res.headers.vary).toMatch(/Origin/i);
  });

  it("preflight от разрешённого Origin - 204 с методами и заголовками", async () => {
    const res = await setup().call("/api/ping", {
      method: "OPTIONS",
      headers: { origin: GOOD, "access-control-request-method": "PUT" },
    });
    expect(res.statusCode).toBe(204);
    expect(res.body).toBe("");
    expect(res.headers["access-control-allow-origin"]).toBe(GOOD);
    expect(res.headers["access-control-allow-methods"]).toBe("GET, POST, PUT, DELETE");
    expect(res.headers["access-control-allow-headers"]).toBe("Content-Type, Authorization");
    expect(res.headers["access-control-max-age"]).toBe("600");
  });

  it("preflight от чужого Origin - 403 в общем формате", async () => {
    const res = await setup().call("/api/ping", {
      method: "OPTIONS",
      headers: { origin: "https://evil.example", "access-control-request-method": "GET" },
    });
    expect(res.statusCode).toBe(403);
    expect(res.json()).toEqual({ error: { code: "origin_not_allowed", message: "Origin is not allowed" } });
    expect(res.headers["access-control-allow-origin"]).toBeUndefined();
  });
});

describe("Ограничение частоты", () => {
  it("после лимита - 429 с Retry-After", async () => {
    const { call } = setup();
    for (let i = 0; i < 3; i += 1) expect((await call("/api/ping")).statusCode).toBe(200);
    const res = await call("/api/ping");
    expect(res.statusCode).toBe(429);
    expect(res.headers["retry-after"]).toBe("10");
    expect(res.json()).toEqual({ error: { code: "rate_limited", message: "Too many requests" } });
  });

  it("заголовки RateLimit-* отражают остаток и время до сброса", async () => {
    const { call, clock } = setup();
    let res = await call("/api/ping");
    expect([res.headers["ratelimit-limit"], res.headers["ratelimit-remaining"], res.headers["ratelimit-reset"]]).toEqual(["3", "2", "10"]);
    clock.t += 2500;
    res = await call("/api/ping");
    expect([res.headers["ratelimit-remaining"], res.headers["ratelimit-reset"]]).toEqual(["1", "8"]);
    await call("/api/ping");
    res = await call("/api/ping");
    expect(res.statusCode).toBe(429);
    expect(res.headers["ratelimit-remaining"]).toBe("0");
    expect(res.headers["retry-after"]).toBe("8");
  });

  it("счётчик сбрасывается по истечении окна (время подаётся снаружи)", async () => {
    const { call, clock } = setup();
    for (let i = 0; i < 3; i += 1) await call("/api/ping");
    expect((await call("/api/ping")).statusCode).toBe(429);
    clock.t += 9999;
    expect((await call("/api/ping")).statusCode).toBe(429);
    clock.t += 1;
    const res = await call("/api/ping");
    expect(res.statusCode).toBe(200);
    expect(res.headers["ratelimit-remaining"]).toBe("2");
  });

  it("клиенты считаются раздельно", async () => {
    const { call } = setup();
    for (let i = 0; i < 4; i += 1) await call("/api/ping", { ip: "10.0.0.1" });
    expect((await call("/api/ping", { ip: "10.0.0.1" })).statusCode).toBe(429);
    expect((await call("/api/ping", { ip: "10.0.0.2" })).statusCode).toBe(200);
  });

  it("лимит действует только на /api, а два приложения не делят счётчики", async () => {
    const a = setup();
    const b = setup();
    for (let i = 0; i < 4; i += 1) await a.call("/api/ping");
    expect((await a.call("/api/ping")).statusCode).toBe(429);
    expect((await a.call("/")).statusCode).toBe(200);
    expect((await b.call("/api/ping")).statusCode).toBe(200);
  });

  it("свой keyOf и ограничение, вызванное напрямую", () => {
    const limiter = rateLimit({ limit: 1, windowMs: 1000, now: () => 5, keyOf: (req) => req.user });
    const statuses = [];
    const run = (user) => {
      const res = {
        headers: {},
        set(name, value) { this.headers[name] = value; return this; },
        status(code) { statuses.push(code); return this; },
        json() { return this; },
      };
      let passed = false;
      limiter({ user }, res, () => { passed = true; });
      return passed;
    };
    expect(run("ann")).toBe(true);
    expect(run("bob")).toBe(true);
    expect(run("ann")).toBe(false);
    expect(statuses).toEqual([429]);
  });
});

describe("Cookie сессии", () => {
  it("cookie sid: HttpOnly, Secure, SameSite=Lax, Path=/ и Max-Age=3600", async () => {
    const res = await setup().call("/login", { method: "POST" });
    expect(res.statusCode).toBe(204);
    const header = [].concat(res.headers["set-cookie"] ?? []).find((c) => c.startsWith("sid="));
    expect(header).toBeDefined();
    expect(header).toMatch(/^sid=abc123;/);
    expect(header).toMatch(/;\s*HttpOnly/);
    expect(header).toMatch(/;\s*Secure/);
    expect(header).toMatch(/;\s*SameSite=Lax/);
    expect(header).toMatch(/;\s*Path=\//);
    expect(header).toMatch(/;\s*Max-Age=3600/);
  });
});
