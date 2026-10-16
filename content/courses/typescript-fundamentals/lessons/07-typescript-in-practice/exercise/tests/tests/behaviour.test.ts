import { describe, it, expect } from "vitest";
import { Status, isStatus, label, Counter } from "../src/status.ts";
import { nth, lastOf, head, withRetries, retriesOf } from "../src/list.ts";
import { clampAll, average, libVersion } from "../src/stats.ts";

describe("Status", () => {
  it("значения и isStatus", () => {
    expect(Status.Active).toBe("active");
    expect(Object.values(Status).sort()).toEqual(["active", "blocked", "pending"]);
    expect(isStatus("blocked")).toBe(true);
    expect(isStatus("Active")).toBe(false);
    expect(isStatus(0)).toBe(false);
    expect(isStatus(undefined)).toBe(false);
  });
  it("label для каждого статуса", () => {
    expect(label(Status.Active)).toBe("Активен");
    expect(label(Status.Blocked)).toBe("Заблокирован");
    expect(label(Status.Pending)).toBe("Ожидает");
  });
  it("label: неизвестное значение во время работы - исключение", () => {
    expect(() => label("deleted" as never)).toThrow();
  });
});

describe("Counter", () => {
  it("значение по умолчанию и increment не изменяет исходный", () => {
    const c = new Counter();
    expect(c.value).toBe(0);
    const d = c.increment();
    expect(d.value).toBe(1);
    expect(c.value).toBe(0);
    expect(new Counter(5).increment().value).toBe(6);
  });
});

describe("списки", () => {
  it("nth: индекс внутри и вне массива", () => {
    expect(nth(["a", "b"], 0)).toBe("a");
    expect(nth(["a", "b"], 2)).toBeUndefined();
    expect(nth(["a"], -1)).toBeUndefined();
  });
  it("lastOf: последний элемент и пустой массив", () => {
    expect(lastOf([1, 2, 3])).toBe(3);
    expect(lastOf([])).toBeUndefined();
    expect(lastOf([0])).toBe(0);
  });
  it("head: первый элемент", () => {
    expect(head([7, 8])).toBe(7);
    expect(head([""])).toBe("");
  });
});

describe("Options", () => {
  it("withRetries: значение и удаление ключа", () => {
    expect(withRetries({}, 2)).toEqual({ retries: 2 });
    const cleared = withRetries({ retries: 2 }, undefined);
    expect("retries" in cleared).toBe(false);
  });
  it("withRetries: не изменяет исходный объект", () => {
    const o = { retries: 1 };
    withRetries(o, 5);
    withRetries(o, undefined);
    expect(o).toEqual({ retries: 1 });
  });
  it("retriesOf: 0 - допустимое значение", () => {
    expect(retriesOf({})).toBe(3);
    expect(retriesOf({ retries: 0 })).toBe(0);
    expect(retriesOf({ retries: 7 })).toBe(7);
  });
});

describe("legacy-math", () => {
  it("clampAll, average, libVersion", () => {
    expect(clampAll([-5, 5, 50], 0, 10)).toEqual([0, 5, 10]);
    expect(average([2, 4])).toBe(3);
    expect(average([])).toBe(0);
    expect(libVersion()).toBe("1.2.0");
  });
});
