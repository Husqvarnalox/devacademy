import { NextResponse, type NextRequest } from "next/server";
// Для TODO 3: DEFAULT_LOCALE, isLocale(value) и тип Locale из ./lib/locales.
import { DEFAULT_LOCALE, isLocale, type Locale } from "./lib/locales";

// TODO: перечислите адреса, для которых Proxy вызывается:
// /old-blog/..., /dashboard/... (включая сам /dashboard) и /docs/...
export const config = {
  matcher: [],
};

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  // TODO 1: /old-blog/... -> редирект с кодом 308 на тот же путь с /blog вместо /old-blog (query сохраняется).

  // TODO 2: /dashboard/... без непустой cookie session -> редирект на /login?from=<путь и query>.
  // С cookie запрос идёт дальше.

  // TODO 3: /docs/... -> язык из первого сегмента (ru или en), иначе из cookie NEXT_LOCALE,
  // иначе DEFAULT_LOCALE. Если языка нет в адресе, перепишите адрес на /docs/<язык>/...

  // TODO 4: у каждого ответа, который не редирект, заголовок x-request-path с исходным путём;
  // на /docs дальше по цепочке передаётся заголовок запроса x-locale.

  return NextResponse.next();
}
