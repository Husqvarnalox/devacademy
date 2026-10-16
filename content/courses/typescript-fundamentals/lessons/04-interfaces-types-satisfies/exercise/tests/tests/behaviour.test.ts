import { describe, it, expect } from "vitest";
import { ROUTES, buildUrl, resolveTheme, withTheme, limitOf, countBy, type Settings } from "../src/settings.ts";

const base: Settings = { name: "app", limits: { rps: 10, burst: 3 } };

describe("buildUrl", () => {
  it("маршрут без параметров", () => {
    expect(buildUrl("home")).toBe("/");
    expect(Object.keys(ROUTES).sort()).toEqual(["home", "post", "user"]);
  });
  it("подставляет параметры", () => {
    expect(buildUrl("user", { id: 42 })).toBe("/users/42");
    expect(buildUrl("post", { id: "ada", postId: 7 })).toBe("/users/ada/posts/7");
  });
  it("нулевой и пустой параметры - это значения", () => {
    expect(buildUrl("user", { id: 0 })).toBe("/users/0");
    expect(buildUrl("user", { id: "" })).toBe("/users/");
  });
  it("неизвестные :ключи остаются как есть", () => {
    expect(buildUrl("post", { id: 1 })).toBe("/users/1/posts/:postId");
    expect(buildUrl("user")).toBe("/users/:id");
  });
});

describe("настройки", () => {
  it("resolveTheme: по умолчанию light", () => {
    expect(resolveTheme(base)).toBe("light");
    expect(resolveTheme({ ...base, theme: "dark" })).toBe("dark");
  });
  it("withTheme не изменяет исходный объект", () => {
    const next = withTheme(base, "dark");
    expect(next.theme).toBe("dark");
    expect(next).not.toBe(base);
    expect(base.theme).toBeUndefined();
    expect(next.name).toBe("app");
    expect(next.limits).toEqual({ rps: 10, burst: 3 });
  });
  it("limitOf: значение или 0", () => {
    expect(limitOf(base, "rps")).toBe(10);
    expect(limitOf(base, "missing")).toBe(0);
  });
  it("limitOf: нулевой лимит остаётся нулём, а не заменяется", () => {
    expect(limitOf({ ...base, limits: { rps: 0 } }, "rps")).toBe(0);
  });
});

describe("countBy", () => {
  it("считает слова", () => {
    expect(countBy(["a", "b", "a", "c", "a"])).toEqual({ a: 3, b: 1, c: 1 });
  });
  it("пустой список", () => {
    expect(countBy([])).toEqual({});
  });
  it("не изменяет вход и не ломается на служебных именах", () => {
    const words = ["x", "x"];
    countBy(words);
    expect(words).toEqual(["x", "x"]);
    const r = countBy(["constructor", "constructor"]);
    expect(r["constructor"]).toBe(2);
  });
});
