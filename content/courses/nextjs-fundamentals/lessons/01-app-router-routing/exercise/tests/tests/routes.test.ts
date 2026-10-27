// @vitest-environment node
import { describe, it, expect } from "vitest";
import { routesFromFiles, matchRoute, resolveRoute, layoutsFor } from "../src/routes";

describe("routesFromFiles", () => {
  it("превращает файлы page в URL-шаблоны и пропускает остальные", () => {
    const files = ["page.tsx", "blog/page.tsx", "blog/[slug]/page.tsx", "about/page.js", "dashboard/layout.tsx", "blog/loading.tsx"];
    expect(routesFromFiles(files)).toEqual(["/", "/about", "/blog", "/blog/[slug]"]);
  });

  it("убирает группы маршрутов из адреса", () => {
    const files = ["(shop)/cart/page.tsx", "(shop)/layout.tsx", "(marketing)/about/page.tsx", "(marketing)/(promo)/sale/page.tsx"];
    expect(routesFromFiles(files)).toEqual(["/about", "/cart", "/sale"]);
  });

  it("группа в корне даёт главную страницу", () => {
    expect(routesFromFiles(["(marketing)/page.tsx"])).toEqual(["/"]);
  });

  it("игнорирует приватные папки вместе со всем содержимым", () => {
    const files = ["_components/Button.tsx", "_admin/users/page.tsx", "blog/_lib/page.tsx", "blog/page.tsx", "(shop)/_ui/route.ts"];
    expect(routesFromFiles(files)).toEqual(["/blog"]);
  });

  it("считает маршрутом route.ts и route.js", () => {
    const files = ["api/items/route.ts", "api/items/[id]/route.js", "api/items/helpers.ts"];
    expect(routesFromFiles(files)).toEqual(["/api/items", "/api/items/[id]"]);
  });

  it("не принимает файлы с похожими именами за page и route", () => {
    const files = ["pages.tsx", "Page.tsx", "page.module.css", "mypage.tsx", "page.test.tsx", "blog/route.tsx", "blog/page.d.ts"];
    expect(routesFromFiles(files)).toEqual([]);
  });

  it("возвращает отсортированный список без повторов", () => {
    const files = ["cart/page.tsx", "blog/page.tsx", "page.tsx", "blog/page.tsx", "api/ping/route.ts"];
    expect(routesFromFiles(files)).toEqual(["/", "/api/ping", "/blog", "/cart"]);
  });

  it("сохраняет динамические сегменты и кириллические папки", () => {
    const files = ["docs/[[...slug]]/page.tsx", "shop/[...slug]/page.tsx", "блог/[slug]/page.tsx"];
    expect(routesFromFiles(files)).toEqual(["/docs/[[...slug]]", "/shop/[...slug]", "/блог/[slug]"]);
  });

  it("пустой список файлов даёт пустой список маршрутов", () => {
    expect(routesFromFiles([])).toEqual([]);
  });

  it("бросает ошибку, если в одной папке лежат page и route", () => {
    expect(() => routesFromFiles(["status/page.tsx", "status/route.ts"])).toThrow(Error);
    expect(() => routesFromFiles(["status/page.tsx", "status/route.ts"])).toThrow("/status");
  });

  it("бросает ошибку, если две группы дают один и тот же адрес", () => {
    expect(() => routesFromFiles(["(a)/about/page.tsx", "(b)/about/page.tsx"])).toThrow("/about");
    expect(() => routesFromFiles(["page.tsx", "(marketing)/page.tsx"])).toThrow(Error);
  });
});

describe("matchRoute", () => {
  it("извлекает параметр из динамического сегмента", () => {
    expect(matchRoute("/blog/[slug]", "/blog/hello")).toEqual({ slug: "hello" });
    expect(matchRoute("/blog/[slug]", "/blog")).toBeNull();
    expect(matchRoute("/blog/[slug]", "/blog/a/b")).toBeNull();
  });

  it("статический шаблон возвращает пустой объект параметров", () => {
    expect(matchRoute("/about", "/about")).toEqual({});
    expect(matchRoute("/about", "/contact")).toBeNull();
  });

  it("catch-all собирает один и больше сегментов в массив", () => {
    expect(matchRoute("/shop/[...slug]", "/shop/a/b/c")).toEqual({ slug: ["a", "b", "c"] });
    expect(matchRoute("/shop/[...slug]", "/shop/a")).toEqual({ slug: ["a"] });
    expect(matchRoute("/shop/[...slug]", "/shop")).toBeNull();
  });

  it("optional catch-all допускает ноль сегментов без ключа в параметрах", () => {
    const empty = matchRoute("/docs/[[...slug]]", "/docs");
    expect(empty).toEqual({});
    expect(empty).not.toHaveProperty("slug");
    expect(matchRoute("/docs/[[...slug]]", "/docs/a/b")).toEqual({ slug: ["a", "b"] });
    expect(matchRoute("/docs/[[...slug]]", "/blog")).toBeNull();
  });

  it("не различает адрес с завершающим слэшем и без него", () => {
    expect(matchRoute("/blog/[slug]", "/blog/hello/")).toEqual({ slug: "hello" });
    expect(matchRoute("/", "/")).toEqual({});
    expect(matchRoute("/", "/blog")).toBeNull();
  });

  it("работает с кириллицей и процентным кодированием", () => {
    expect(matchRoute("/блог/[slug]", "/блог/привет-мир")).toEqual({ slug: "привет-мир" });
    const encoded = "/" + encodeURIComponent("блог") + "/" + encodeURIComponent("привет мир");
    expect(matchRoute("/блог/[slug]", encoded)).toEqual({ slug: "привет мир" });
  });

  it("собирает несколько параметров разных видов", () => {
    expect(matchRoute("/shop/[category]/item/[id]", "/shop/books/item/42")).toEqual({ category: "books", id: "42" });
    expect(matchRoute("/[lang]/docs/[...path]", "/ru/docs/a/b")).toEqual({ lang: "ru", path: ["a", "b"] });
    expect(matchRoute("/shop/[category]/item/[id]", "/shop/books/goods/42")).toBeNull();
  });

  it("учитывает регистр статических сегментов", () => {
    expect(matchRoute("/about", "/About")).toBeNull();
  });
});

describe("resolveRoute", () => {
  it("статический шаблон важнее динамического", () => {
    const patterns = ["/blog/[slug]", "/blog/new", "/blog"];
    expect(resolveRoute(patterns, "/blog/new")).toEqual({ pattern: "/blog/new", params: {} });
    expect(resolveRoute(patterns, "/blog/other")).toEqual({ pattern: "/blog/[slug]", params: { slug: "other" } });
  });

  it("динамический шаблон важнее catch-all", () => {
    const patterns = ["/shop/[...rest]", "/shop/[id]"];
    expect(resolveRoute(patterns, "/shop/5")).toEqual({ pattern: "/shop/[id]", params: { id: "5" } });
    expect(resolveRoute(patterns, "/shop/5/6")).toEqual({ pattern: "/shop/[...rest]", params: { rest: ["5", "6"] } });
  });

  it("результат не зависит от порядка шаблонов", () => {
    const a = ["/blog/[slug]", "/blog/new", "/[section]/new", "/[...all]"];
    const b = [...a].reverse();
    expect(resolveRoute(a, "/blog/new")?.pattern).toBe("/blog/new");
    expect(resolveRoute(b, "/blog/new")?.pattern).toBe("/blog/new");
    expect(resolveRoute(a, "/news/new")?.pattern).toBe("/[section]/new");
    expect(resolveRoute(b, "/news/new")?.pattern).toBe("/[section]/new");
  });

  it("возвращает null, если ничего не подошло", () => {
    expect(resolveRoute(["/blog/[slug]", "/about"], "/contact")).toBeNull();
    expect(resolveRoute([], "/")).toBeNull();
  });

  it("сегменты сравниваются слева направо", () => {
    const patterns = ["/[lang]/about", "/blog/[slug]"];
    expect(resolveRoute(patterns, "/blog/about")).toEqual({ pattern: "/blog/[slug]", params: { slug: "about" } });
  });

  it("optional catch-all уступает динамическому сегменту, но принимает адрес без хвоста", () => {
    const patterns = ["/docs/[[...slug]]", "/docs/[id]"];
    expect(resolveRoute(patterns, "/docs/intro")).toEqual({ pattern: "/docs/[id]", params: { id: "intro" } });
    expect(resolveRoute(patterns, "/docs")).toEqual({ pattern: "/docs/[[...slug]]", params: {} });
  });

  it("находит маршрут для кириллического адреса", () => {
    const patterns = ["/блог/[slug]", "/блог/новое"];
    expect(resolveRoute(patterns, "/блог/новое")?.pattern).toBe("/блог/новое");
    expect(resolveRoute(patterns, "/блог/привет")).toEqual({ pattern: "/блог/[slug]", params: { slug: "привет" } });
  });
});

describe("layoutsFor", () => {
  it("возвращает макеты от корня вглубь", () => {
    const files = ["layout.tsx", "blog/layout.tsx", "blog/[slug]/page.tsx", "blog/[slug]/layout.tsx", "about/layout.tsx"];
    expect(layoutsFor(files, "blog/[slug]/page.tsx")).toEqual(["layout.tsx", "blog/layout.tsx", "blog/[slug]/layout.tsx"]);
  });

  it("включает макеты групп маршрутов", () => {
    const files = ["layout.tsx", "(shop)/layout.tsx", "(shop)/cart/page.tsx", "(marketing)/layout.tsx"];
    expect(layoutsFor(files, "(shop)/cart/page.tsx")).toEqual(["layout.tsx", "(shop)/layout.tsx"]);
  });

  it("для страницы в корне берёт только корневой макет", () => {
    expect(layoutsFor(["layout.tsx", "blog/layout.tsx", "page.tsx"], "page.tsx")).toEqual(["layout.tsx"]);
  });

  it("возвращает пустой список, если макетов нет", () => {
    expect(layoutsFor(["blog/page.tsx", "page.tsx"], "blog/page.tsx")).toEqual([]);
  });

  it("не путает папки с похожими именами и файлы с похожими именами", () => {
    const files = ["blog/layout.tsx", "blog-archive/layout.tsx", "blog2/layout.tsx", "blog/mylayout.tsx", "blog/layout-utils.tsx", "blog/layouts.tsx"];
    expect(layoutsFor(files, "blog/page.tsx")).toEqual(["blog/layout.tsx"]);
  });

  it("при нескольких корневых макетах берёт макеты своей ветки", () => {
    const files = ["(shop)/layout.tsx", "(marketing)/layout.tsx", "(shop)/cart/page.tsx"];
    expect(layoutsFor(files, "(shop)/cart/page.tsx")).toEqual(["(shop)/layout.tsx"]);
    expect(layoutsFor(files, "(marketing)/about/page.tsx")).toEqual(["(marketing)/layout.tsx"]);
  });

  it("принимает макеты с расширениями js и jsx", () => {
    const files = ["layout.js", "docs/layout.jsx", "docs/intro/page.js"];
    expect(layoutsFor(files, "docs/intro/page.js")).toEqual(["layout.js", "docs/layout.jsx"]);
  });
});
