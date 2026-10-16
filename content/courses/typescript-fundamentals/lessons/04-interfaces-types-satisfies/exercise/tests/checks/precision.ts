import { ROUTES, buildUrl, resolveTheme, withTheme, limitOf, countBy, type Settings, type RouteName, type Theme } from "../src/settings.ts";

// theme необязательна: объект без неё допустим.
export const s: Settings = { name: "app", limits: { rps: 10 } };
export const s2: Settings = { name: "app", theme: "dark", limits: {} };

// name и limits - только для чтения (и вложенные значения limits тоже).
// @ts-expect-error name объявлен readonly
s.name = "other";
// @ts-expect-error limits объявлен readonly
s.limits = {};
// @ts-expect-error значения в limits нельзя менять
s.limits["rps"] = 1;
// theme менять можно.
s.theme = "dark";
// @ts-expect-error неизвестная тема
s.theme = "blue";

// Имена маршрутов - объединение литералов, а не string.
export const ok1: RouteName = "home";
export const ok2: RouteName = "post";
// @ts-expect-error такого маршрута нет
export const bad1: RouteName = "about";
// @ts-expect-error buildUrl принимает только известные имена
buildUrl("about");
buildUrl("user", { id: 1 });

// satisfies сохраняет форму объекта: у ROUTES ровно эти ключи.
export const home: string = ROUTES.home;
// @ts-expect-error ключа about нет в ROUTES
export const nope = ROUTES.about;

// Результаты функций.
export const t: Theme = resolveTheme(s);
export const next: Settings = withTheme(s, "light");
export const lim: number = limitOf(s, "rps");
export const counts: Record<string, number> = countBy(["a", "b"]);
// @ts-expect-error countBy принимает строки
countBy([1, 2]);
// @ts-expect-error withTheme принимает только тему
withTheme(s, "red");
