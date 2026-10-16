import { describe, it, expect } from "vitest";
import { first, pluck, groupBy, ok, err, mapResult, unwrapOr } from "../src/collections.ts";

const users = [
  { name: "Ада", age: 36, admin: true },
  { name: "Билл", age: 29, admin: false },
  { name: "Грейс", age: 36, admin: false },
];

describe("first", () => {
  it("первый элемент и пустой массив", () => {
    expect(first([3, 4])).toBe(3);
    expect(first([])).toBeUndefined();
  });
  it("ложные элементы возвращаются как есть", () => {
    expect(first([0, 1])).toBe(0);
    expect(first(["", "a"])).toBe("");
    expect(first([false])).toBe(false);
  });
});

describe("pluck", () => {
  it("значения поля", () => {
    expect(pluck(users, "name")).toEqual(["Ада", "Билл", "Грейс"]);
    expect(pluck(users, "age")).toEqual([36, 29, 36]);
  });
  it("пустой массив и undefined в поле", () => {
    expect(pluck([], "x" as never)).toEqual([]);
    expect(pluck([{ a: undefined }, { a: 1 }], "a")).toEqual([undefined, 1]);
  });
});

describe("groupBy", () => {
  it("группирует и сохраняет порядок", () => {
    const g = groupBy(users, (u) => u.age);
    expect([...g.keys()]).toEqual([36, 29]);
    expect(g.get(36)?.map((u) => u.name)).toEqual(["Ада", "Грейс"]);
    expect(g.get(29)?.map((u) => u.name)).toEqual(["Билл"]);
  });
  it("ключи любых типов: boolean, 0, пустая строка", () => {
    const g = groupBy([0, 1, 2, 3], (n) => n % 2 === 0);
    expect(g.get(true)).toEqual([0, 2]);
    expect(g.get(false)).toEqual([1, 3]);
    const h = groupBy(["", "a", ""], (s) => s);
    expect(h.get("")).toEqual(["", ""]);
  });
  it("пустой вход и отсутствие побочных эффектов", () => {
    expect(groupBy([], (x) => x).size).toBe(0);
    const input = [1, 2];
    groupBy(input, (n) => n);
    expect(input).toEqual([1, 2]);
  });
});

describe("Result", () => {
  it("ok и err", () => {
    expect(ok(5)).toEqual({ ok: true, value: 5 });
    expect(err("x")).toEqual({ ok: false, error: "x" });
  });
  it("mapResult применяет функцию только к успеху", () => {
    expect(mapResult(ok(2), (n) => n * 10)).toEqual({ ok: true, value: 20 });
    let calls = 0;
    const e = err("boom");
    expect(
      mapResult(e, (n: number) => {
        calls++;
        return n;
      }),
    ).toEqual({ ok: false, error: "boom" });
    expect(calls).toBe(0);
  });
  it("unwrapOr: значение, в том числе 0 и пустая строка, или запасное", () => {
    expect(unwrapOr(ok(7), 0)).toBe(7);
    expect(unwrapOr(ok(0), 5)).toBe(0);
    expect(unwrapOr(ok(""), "z")).toBe("");
    expect(unwrapOr(err("x") as ReturnType<typeof err<string>> | ReturnType<typeof ok<number>>, 9)).toBe(9);
  });
});
