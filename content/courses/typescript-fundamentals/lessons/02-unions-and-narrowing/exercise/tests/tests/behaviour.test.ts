import { describe, it, expect } from "vitest";
import { area, describe as describeShape, formatInput, assertNever } from "../src/shapes.ts";

describe("area", () => {
  it("круг: pi * r^2", () => {
    expect(area({ kind: "circle", r: 2 })).toBeCloseTo(12.566370614, 6);
  });
  it("прямоугольник и квадрат", () => {
    expect(area({ kind: "rect", w: 3, h: 4 })).toBe(12);
    expect(area({ kind: "square", side: 5 })).toBe(25);
  });
  it("нулевые размеры дают 0", () => {
    expect(area({ kind: "circle", r: 0 })).toBe(0);
    expect(area({ kind: "rect", w: 0, h: 9 })).toBe(0);
  });
  it("неизвестный вид во время работы приводит к исключению", () => {
    expect(() => area({ kind: "hexagon", side: 1 } as never)).toThrow();
  });
});

describe("describe", () => {
  it("текстовые описания", () => {
    expect(describeShape({ kind: "circle", r: 2 })).toBe("круг r=2");
    expect(describeShape({ kind: "rect", w: 3, h: 4 })).toBe("прямоугольник 3x4");
    expect(describeShape({ kind: "square", side: 5 })).toBe("квадрат 5");
  });
});

describe("formatInput", () => {
  it("строка, число, логическое", () => {
    expect(formatInput("abc")).toBe("'abc'");
    expect(formatInput(3.14159)).toBe("3.14");
    expect(formatInput(true)).toBe("да");
    expect(formatInput(false)).toBe("нет");
  });
  it("null, undefined и массив", () => {
    expect(formatInput(null)).toBe("null");
    expect(formatInput(undefined)).toBe("нет значения");
    expect(formatInput(["a", "b"])).toBe("[a, b]");
  });
  it("ложные значения не путаются с отсутствием", () => {
    expect(formatInput("")).toBe("''");
    expect(formatInput(0)).toBe("0.00");
    expect(formatInput(false)).toBe("нет");
    expect(formatInput([])).toBe("[]");
  });
});

describe("assertNever", () => {
  it("бросает исключение с описанием значения", () => {
    expect(() => assertNever("x" as never)).toThrow(/x/);
  });
});
