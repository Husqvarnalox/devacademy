import { describe, it, expect } from "vitest";
import { str, num, bool, literal, arr, optional, oneOf, obj } from "../src/guards.ts";

describe("простые проверки", () => {
  it("str, num, bool", () => {
    expect(str()("a")).toBe(true);
    expect(str()("")).toBe(true);
    expect(str()(1)).toBe(false);
    expect(num()(0)).toBe(true);
    expect(num()(-1.5)).toBe(true);
    expect(num()("1")).toBe(false);
    expect(bool()(false)).toBe(true);
    expect(bool()(0)).toBe(false);
  });
  it("num отвергает NaN и бесконечности", () => {
    expect(num()(NaN)).toBe(false);
    expect(num()(Infinity)).toBe(false);
    expect(num()(-Infinity)).toBe(false);
  });
  it("literal сравнивает строго", () => {
    expect(literal("a")("a")).toBe(true);
    expect(literal("a")("b")).toBe(false);
    expect(literal(0)(0)).toBe(true);
    expect(literal(0)("0")).toBe(false);
    expect(literal(false)(false)).toBe(true);
    expect(literal(false)(0)).toBe(false);
  });
});

describe("составные проверки", () => {
  it("arr проверяет каждый элемент", () => {
    expect(arr(str())([])).toBe(true);
    expect(arr(str())(["a", "b"])).toBe(true);
    expect(arr(str())(["a", 1])).toBe(false);
    expect(arr(num())("12")).toBe(false);
    expect(arr(arr(num()))([[1], [], [2, 3]])).toBe(true);
    expect(arr(arr(num()))([[1], ["x"]])).toBe(false);
  });
  it("optional принимает undefined, но не null", () => {
    expect(optional(str())(undefined)).toBe(true);
    expect(optional(str())("x")).toBe(true);
    expect(optional(str())(null)).toBe(false);
    expect(optional(str())(1)).toBe(false);
  });
  it("oneOf: хотя бы один вариант", () => {
    const g = oneOf(str(), num());
    expect(g("a")).toBe(true);
    expect(g(1)).toBe(true);
    expect(g(true)).toBe(false);
    expect(oneOf()(1)).toBe(false);
  });
});

describe("obj", () => {
  const user = obj({ id: num(), name: str(), tags: arr(str()), nick: optional(str()) });
  it("принимает подходящий объект, в том числе с лишними полями", () => {
    expect(user({ id: 1, name: "Ада", tags: [], nick: "ada" })).toBe(true);
    expect(user({ id: 1, name: "Ада", tags: ["x"], extra: 42 })).toBe(true);
  });
  it("отсутствующее необязательное поле допустимо, обязательное - нет", () => {
    expect(user({ id: 1, name: "Ада", tags: [] })).toBe(true);
    expect(user({ id: 1, tags: [] })).toBe(false);
  });
  it("не объекты отвергаются", () => {
    for (const bad of [null, undefined, 1, "x", [], () => 1]) expect(user(bad)).toBe(false);
  });
  it("вложенные объекты", () => {
    const g = obj({ a: obj({ b: arr(num()) }) });
    expect(g({ a: { b: [1, 2] } })).toBe(true);
    expect(g({ a: { b: [1, "2"] } })).toBe(false);
    expect(g({ a: null })).toBe(false);
  });
  it("пустая форма принимает любой объект", () => {
    expect(obj({})({})).toBe(true);
    expect(obj({})({ a: 1 })).toBe(true);
    expect(obj({})(null)).toBe(false);
  });
  it("унаследованные свойства не считаются полями", () => {
    const g = obj({ constructor: optional(str()) });
    expect(g({})).toBe(true);
    expect(obj({ toString: str() })({})).toBe(false);
  });
});
