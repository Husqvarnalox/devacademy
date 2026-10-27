import { NextResponse, type NextRequest } from "next/server";
import { DEFAULT_LOCALE, isLocale, type Locale } from "./lib/locales";

// Proxy вызывается только для этих адресов: остальные запросы он не видит.
export const config = {
  matcher: ["/old-blog/:path*", "/dashboard/:path*", "/docs/:path*"],
};

// Исходный адрес запроса попадает в заголовок ответа, даже если путь был переписан.
function withRequestPath(response: NextResponse, pathname: string): NextResponse {
  response.headers.set("x-request-path", pathname);
  return response;
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (pathname.startsWith("/old-blog")) {
    const target = request.nextUrl.clone();
    target.pathname = pathname.replace("/old-blog", "/blog");
    return NextResponse.redirect(target, 308);
  }

  if (pathname.startsWith("/dashboard")) {
    if (!request.cookies.get("session")?.value) {
      const login = new URL("/login", request.url);
      login.searchParams.set("from", pathname + search);
      return NextResponse.redirect(login);
    }
    return withRequestPath(NextResponse.next(), pathname);
  }

  // Остался только /docs: язык берётся из адреса, иначе из cookie, иначе по умолчанию.
  const first = pathname.split("/")[2];
  const cookieLocale = request.cookies.get("NEXT_LOCALE")?.value;
  const locale: Locale = isLocale(first)
    ? first
    : isLocale(cookieLocale)
      ? cookieLocale
      : DEFAULT_LOCALE;

  const headers = new Headers(request.headers);
  headers.set("x-locale", locale);

  if (isLocale(first)) {
    return withRequestPath(NextResponse.next({ request: { headers } }), pathname);
  }

  const target = request.nextUrl.clone();
  target.pathname = `/docs/${locale}${pathname.slice("/docs".length)}`;
  return withRequestPath(NextResponse.rewrite(target, { request: { headers } }), pathname);
}
