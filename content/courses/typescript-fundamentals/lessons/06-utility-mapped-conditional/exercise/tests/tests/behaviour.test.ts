import { describe, it, expect } from "vitest";
import { pickKeys, omitKeys, merge } from "../src/transform.ts";

const user = { id: 1, name: "Ада", age: 36, email: undefined as string | undefined };

describe("pickKeys", () => {
  it("оставляет только указанные ключи", () => {
    expect(pickKeys(user, ["name", "age"])).toEqual({ name: "Ада", age: 36 });
    expect(pickKeys(user, [])).toEqual({});
  });
  it("не изменяет исходный объект и не добавляет лишнего", () => {
    const copy = { ...user };
    const r = pickKeys(user, ["id"]);
    expect(r).toEqual({ id: 1 });
    expect(Object.keys(r)).toEqual(["id"]);
    expect(user).toEqual(copy);
  });
  it("ключ со значением undefined сохраняется, отсутствующий ключ не создаётся", () => {
    expect(Object.keys(pickKeys(user, ["email"]))).toEqual(["email"]);
    const partial: { a?: number; b?: number } = { a: 1 };
    expect(Object.keys(pickKeys(partial, ["a", "b"]))).toEqual(["a"]);
  });
});

describe("omitKeys", () => {
  it("убирает указанные ключи", () => {
    expect(omitKeys({ id: 1, name: "x", age: 3 }, ["id", "age"])).toEqual({ name: "x" });
    expect(omitKeys({ id: 1 }, [])).toEqual({ id: 1 });
  });
  it("возвращает копию, исходный объект не меняется", () => {
    const src = { a: 1, b: 2 };
    const r = omitKeys(src, ["a"]);
    expect(src).toEqual({ a: 1, b: 2 });
    expect(r).not.toBe(src);
    expect(omitKeys(src, [])).not.toBe(src);
  });
});

describe("merge", () => {
  it("значения patch перекрывают base", () => {
    expect(merge({ a: 1, b: 2 }, { b: 5 })).toEqual({ a: 1, b: 5 });
  });
  it("undefined в patch не затирает значение", () => {
    expect(merge({ a: 1, b: 2 }, { a: undefined })).toEqual({ a: 1, b: 2 });
  });
  it("0, пустая строка, false и null применяются", () => {
    expect(merge({ n: 5, s: "x", f: true }, { n: 0, s: "", f: false })).toEqual({ n: 0, s: "", f: false });
    expect(merge<{ v: string | null }>({ v: "a" }, { v: null })).toEqual({ v: null });
  });
  it("исходные объекты не изменяются", () => {
    const base = { a: 1 };
    const patch = { a: 2 };
    const r = merge(base, patch);
    expect(base).toEqual({ a: 1 });
    expect(patch).toEqual({ a: 2 });
    expect(r).not.toBe(base);
  });
});
