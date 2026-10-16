import { describe, it, expect } from "vitest";
import { isRecord, isStringArray, parseUser, parseUserJson, describeError, expectUser, fail } from "../src/parse.ts";

const good = { id: 7, name: "Ада", email: "ada@example.org", tags: ["admin", "dev"] };

describe("предикаты", () => {
  it("isRecord: объект - да; null, массив, строка - нет", () => {
    expect(isRecord({})).toBe(true);
    expect(isRecord({ a: 1 })).toBe(true);
    expect(isRecord(null)).toBe(false);
    expect(isRecord([])).toBe(false);
    expect(isRecord("x")).toBe(false);
    expect(isRecord(undefined)).toBe(false);
  });
  it("isStringArray: только массивы строк", () => {
    expect(isStringArray([])).toBe(true);
    expect(isStringArray(["a", "b"])).toBe(true);
    expect(isStringArray(["a", 1])).toBe(false);
    expect(isStringArray("ab")).toBe(false);
    expect(isStringArray({ length: 0 })).toBe(false);
  });
});

describe("parseUser", () => {
  it("корректный объект принимается", () => {
    expect(parseUser(good)).toEqual({ ok: true, value: good });
  });
  it("email может быть null", () => {
    const r = parseUser({ ...good, email: null });
    expect(r.ok).toBe(true);
  });
  it("лишние поля отбрасываются", () => {
    const r = parseUser({ ...good, password: "secret" });
    expect(r).toEqual({ ok: true, value: good });
  });
  it("не объект: null, массив, число, строка", () => {
    for (const bad of [null, [], 5, "x", undefined]) {
      const r = parseUser(bad);
      expect(r.ok).toBe(false);
    }
  });
  it("каждое поле проверяется, ошибка называет поле", () => {
    const cases: [Record<string, unknown>, string][] = [
      [{ ...good, id: "7" }, "id"],
      [{ ...good, id: 1.5 }, "id"],
      [{ ...good, name: "" }, "name"],
      [{ ...good, name: 5 }, "name"],
      [{ ...good, email: 5 }, "email"],
      [{ ...good, tags: "admin" }, "tags"],
      [{ ...good, tags: ["a", 2] }, "tags"],
    ];
    for (const [input, field] of cases) {
      const r = parseUser(input);
      expect(r.ok).toBe(false);
      if (!r.ok) expect(r.error).toContain(field);
    }
  });
  it("отсутствующие поля - ошибка", () => {
    expect(parseUser({ id: 1, name: "x", email: null }).ok).toBe(false);
    expect(parseUser({ name: "x", email: null, tags: [] }).ok).toBe(false);
  });
  it("результат - копия: изменение исходных tags не влияет", () => {
    const tags = ["a"];
    const r = parseUser({ ...good, tags });
    expect(r.ok).toBe(true);
    tags.push("b");
    if (r.ok) expect(r.value.tags).toEqual(["a"]);
  });
});

describe("parseUserJson", () => {
  it("разбирает корректный JSON", () => {
    expect(parseUserJson(JSON.stringify(good))).toEqual({ ok: true, value: good });
  });
  it("некорректный JSON и пустая строка", () => {
    expect(parseUserJson("{oops")).toEqual({ ok: false, error: "некорректный JSON" });
    expect(parseUserJson("")).toEqual({ ok: false, error: "некорректный JSON" });
  });
  it("корректный JSON неверной формы", () => {
    expect(parseUserJson("[]").ok).toBe(false);
    expect(parseUserJson("null").ok).toBe(false);
    expect(parseUserJson('{"id":1}').ok).toBe(false);
  });
});

describe("describeError и expectUser", () => {
  it("describeError: Error, строка, прочее", () => {
    expect(describeError(new Error("boom"))).toBe("boom");
    expect(describeError("text")).toBe("text");
    expect(describeError(42)).toBe("неизвестная ошибка: 42");
    expect(describeError(null)).toBe("неизвестная ошибка: null");
  });
  it("expectUser возвращает пользователя или бросает исключение", () => {
    expect(expectUser(good)).toEqual(good);
    expect(() => expectUser({ id: "x" })).toThrow(/id/);
    expect(() => fail("stop")).toThrow("stop");
  });
});
