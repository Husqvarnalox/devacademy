// @vitest-environment node
import { describe, expect, it, vi } from "vitest";

// Помощники next/experimental/testing/server подключают внутренности Next.js, которым нужен
// глобальный AsyncLocalStorage. На сервере Next.js его выставляет сам, в Vitest выставляем вручную
// до загрузки модулей (vi.hoisted выполняется раньше импортов).
vi.hoisted(() => {
  (globalThis as Record<string, unknown>).AsyncLocalStorage = require("node:async_hooks").AsyncLocalStorage;
});

import { NextRequest } from "next/server";
import {
  getRedirectUrl,
  getRewrittenUrl,
  isRewrite,
  unstable_doesMiddlewareMatch,
} from "next/experimental/testing/server";
import { config, proxy } from "../src/proxy";

const ORIGIN = "http://localhost:3000";

function request(path: string, init: { cookie?: string; headers?: Record<string, string> } = {}): NextRequest {
  const headers: Record<string, string> = { ...init.headers };
  if (init.cookie) {
    headers.cookie = init.cookie;
  }
  return new NextRequest(`${ORIGIN}${path}`, { headers });
}

function run(path: string, init?: { cookie?: string; headers?: Record<string, string> }) {
  return proxy(request(path, init));
}

function matches(path: string): boolean {
  return unstable_doesMiddlewareMatch({ config, nextConfig: {}, url: path });
}

// Куда ведёт редирект: путь и параметры без адреса сервера.
function redirectTo(response: Response): { pathname: string; params: URLSearchParams; status: number } | null {
  const location = getRedirectUrl(response);
  if (!location) {
    return null;
  }
  const url = new URL(location);
  return { pathname: url.pathname, params: url.searchParams, status: response.status };
}

function rewriteTo(response: Response): string | null {
  if (!isRewrite(response)) {
    return null;
  }
  const url = new URL(getRewrittenUrl(response) ?? "");
  return url.pathname + url.search;
}

// Заголовок запроса, который Proxy передал дальше по цепочке (NextResponse.next({ request: { headers } })).
function forwarded(response: Response, name: string): string | null {
  return response.headers.get(`x-middleware-request-${name}`);
}

describe("Matcher", () => {
  it("срабатывает на старом блоге, кабинете и документации", () => {
    expect(matches("/old-blog/hello")).toBe(true);
    expect(matches("/dashboard/settings")).toBe(true);
    expect(matches("/docs/intro")).toBe(true);
  });

  it("не срабатывает на главной, служебных файлах и API", () => {
    expect(matches("/")).toBe(false);
    expect(matches("/_next/static/chunk.js")).toBe(false);
    expect(matches("/api/users")).toBe(false);
    expect(matches("/blog/hello")).toBe(false);
  });

  it("срабатывает на корне каждого раздела без хвоста (скрытый)", () => {
    expect(matches("/dashboard")).toBe(true);
    expect(matches("/old-blog")).toBe(true);
    expect(matches("/docs")).toBe(true);
  });

  it("не срабатывает на похожих префиксах (скрытый)", () => {
    expect(matches("/dashboards")).toBe(false);
    expect(matches("/old-blogger/x")).toBe(false);
    expect(matches("/docsify")).toBe(false);
  });

  it("срабатывает на глубоких вложенных путях (скрытый)", () => {
    expect(matches("/old-blog/2024/05/post")).toBe(true);
    expect(matches("/dashboard/team/42/edit")).toBe(true);
    expect(matches("/docs/ru/guide/start")).toBe(true);
  });
});

describe("Старые адреса блога", () => {
  it("перенаправляет /old-blog/hello на /blog/hello кодом 308", () => {
    const target = redirectTo(run("/old-blog/hello"));
    expect(target?.status).toBe(308);
    expect(target?.pathname).toBe("/blog/hello");
  });

  it("сохраняет параметры запроса (скрытый)", () => {
    const target = redirectTo(run("/old-blog/hello?utm=mail&page=2"));
    expect(target?.pathname).toBe("/blog/hello");
    expect(target?.params.get("utm")).toBe("mail");
    expect(target?.params.get("page")).toBe("2");
  });

  it("переносит корень раздела на /blog (скрытый)", () => {
    const target = redirectTo(run("/old-blog"));
    expect(target?.status).toBe(308);
    expect(target?.pathname).toBe("/blog");
  });

  it("переносит вложенный путь целиком (скрытый)", () => {
    expect(redirectTo(run("/old-blog/2024/05/post"))?.pathname).toBe("/blog/2024/05/post");
  });

  it("не портит кириллицу в пути (скрытый)", () => {
    const target = redirectTo(run(`/old-blog/${encodeURIComponent("привет-мир")}`));
    expect(target?.pathname).toBe(`/blog/${encodeURIComponent("привет-мир")}`);
  });
});

describe("Кабинет", () => {
  it("без сессии отправляет на /login с адресом возврата", () => {
    const target = redirectTo(run("/dashboard/settings"));
    expect(target).not.toBeNull();
    expect(target?.status).toBe(307);
    expect(target?.pathname).toBe("/login");
    expect(target?.params.get("from")).toBe("/dashboard/settings");
  });

  it("с сессией пропускает запрос дальше", () => {
    const response = run("/dashboard/settings", { cookie: "session=abc123" });
    expect(redirectTo(response)).toBeNull();
    expect(isRewrite(response)).toBe(false);
    expect(response.headers.get("x-middleware-next")).toBe("1");
  });

  it("защищает и сам /dashboard (скрытый)", () => {
    const target = redirectTo(run("/dashboard"));
    expect(target?.pathname).toBe("/login");
    expect(target?.params.get("from")).toBe("/dashboard");
  });

  it("сохраняет query в адресе возврата (скрытый)", () => {
    const target = redirectTo(run("/dashboard/reports?year=2025&q=%D0%BE%D1%82%D1%87%D1%91%D1%82"));
    expect(target?.pathname).toBe("/login");
    expect(target?.params.get("from")).toBe("/dashboard/reports?year=2025&q=%D0%BE%D1%82%D1%87%D1%91%D1%82");
  });

  it("считает пустую cookie отсутствием сессии (скрытый)", () => {
    const target = redirectTo(run("/dashboard", { cookie: "session=" }));
    expect(target?.pathname).toBe("/login");
  });

  it("смотрит именно на cookie session (скрытый)", () => {
    const target = redirectTo(run("/dashboard", { cookie: "sessions=abc; token=xyz" }));
    expect(target?.pathname).toBe("/login");
  });
});

describe("Язык документации", () => {
  it("без cookie переписывает /docs/intro на английский язык по умолчанию", () => {
    expect(rewriteTo(run("/docs/intro"))).toBe("/docs/en/intro");
  });

  it("берёт язык из cookie NEXT_LOCALE (скрытый)", () => {
    expect(rewriteTo(run("/docs/intro", { cookie: "NEXT_LOCALE=ru" }))).toBe("/docs/ru/intro");
  });

  it("при неизвестном языке в cookie выбирает язык по умолчанию (скрытый)", () => {
    expect(rewriteTo(run("/docs/intro", { cookie: "NEXT_LOCALE=de" }))).toBe("/docs/en/intro");
  });

  it("переписывает корень /docs на страницу языка (скрытый)", () => {
    expect(rewriteTo(run("/docs", { cookie: "NEXT_LOCALE=ru" }))).toBe("/docs/ru");
  });

  it("не переписывает адрес, где язык уже есть (скрытый)", () => {
    const response = run("/docs/ru/intro");
    expect(isRewrite(response)).toBe(false);
    expect(redirectTo(response)).toBeNull();
    expect(response.headers.get("x-middleware-next")).toBe("1");
  });

  it("язык из адреса сильнее cookie (скрытый)", () => {
    const response = run("/docs/en/intro", { cookie: "NEXT_LOCALE=ru" });
    expect(isRewrite(response)).toBe(false);
    expect(forwarded(response, "x-locale")).toBe("en");
  });

  it("сохраняет query при переписывании (скрытый)", () => {
    expect(rewriteTo(run("/docs/guide/start?tab=api", { cookie: "NEXT_LOCALE=ru" }))).toBe("/docs/ru/guide/start?tab=api");
  });
});

describe("Заголовки", () => {
  it("кладёт исходный путь в x-request-path для кабинета", () => {
    const response = run("/dashboard/settings", { cookie: "session=abc" });
    expect(response.headers.get("x-request-path")).toBe("/dashboard/settings");
  });

  it("передаёт язык дальше заголовком запроса x-locale (скрытый)", () => {
    const response = run("/docs/intro", { cookie: "NEXT_LOCALE=ru" });
    expect(forwarded(response, "x-locale")).toBe("ru");
  });

  it("не отдаёт x-locale клиенту в заголовках ответа (скрытый)", () => {
    const response = run("/docs/intro", { cookie: "NEXT_LOCALE=ru" });
    expect(response.headers.get("x-locale")).toBeNull();
  });

  it("при переписывании в x-request-path остаётся исходный адрес (скрытый)", () => {
    const response = run("/docs/intro", { cookie: "NEXT_LOCALE=ru" });
    expect(rewriteTo(response)).toBe("/docs/ru/intro");
    expect(response.headers.get("x-request-path")).toBe("/docs/intro");
  });

  it("заменяет x-locale, присланный клиентом (скрытый)", () => {
    const response = run("/docs/intro", { cookie: "NEXT_LOCALE=ru", headers: { "x-locale": "evil" } });
    expect(forwarded(response, "x-locale")).toBe("ru");
  });

  it("сохраняет остальные заголовки запроса (скрытый)", () => {
    const response = run("/docs/ru/intro", { headers: { "x-trace": "abc" } });
    expect(forwarded(response, "x-trace")).toBe("abc");
    expect(response.headers.get("x-request-path")).toBe("/docs/ru/intro");
  });
});
