import { describe, it, expect } from "vitest";
import { distance, centroid, translate, nearest } from "../src/geometry.ts";

describe("distance", () => {
  it("расстояние от (0,0) до (3,4) равно 5", () => {
    expect(distance({ x: 0, y: 0 }, { x: 3, y: 4 })).toBe(5);
  });
  it("симметрично и работает с отрицательными координатами", () => {
    const a = { x: -1, y: -1 };
    const b = { x: 2, y: 3 };
    expect(distance(a, b)).toBe(5);
    expect(distance(b, a)).toBe(5);
    expect(distance(a, a)).toBe(0);
  });
});

describe("centroid", () => {
  it("средняя точка трёх вершин", () => {
    expect(centroid([{ x: 0, y: 0 }, { x: 6, y: 0 }, { x: 0, y: 3 }])).toEqual({ x: 2, y: 1 });
  });
  it("пустой список даёт undefined", () => {
    expect(centroid([])).toBeUndefined();
  });
  it("принимает массив только для чтения и не меняет его", () => {
    const pts = Object.freeze([Object.freeze({ x: 1, y: 1 }), Object.freeze({ x: 3, y: 5 })]);
    expect(centroid(pts)).toEqual({ x: 2, y: 3 });
  });
});

describe("translate", () => {
  it("сдвигает точку", () => {
    expect(translate({ x: 1, y: 2 }, 10, -2)).toEqual({ x: 11, y: 0 });
  });
  it("возвращает новый объект и не меняет исходный", () => {
    const p = Object.freeze({ x: 1, y: 1 });
    const q = translate(p, 0, 0);
    expect(q).not.toBe(p);
    expect(q).toEqual({ x: 1, y: 1 });
  });
});

describe("nearest", () => {
  it("находит ближайшую точку", () => {
    const pts = [{ x: 10, y: 10 }, { x: 1, y: 1 }, { x: -5, y: 0 }];
    expect(nearest(pts, { x: 0, y: 0 })).toEqual({ x: 1, y: 1 });
  });
  it("для пустого списка даёт undefined", () => {
    expect(nearest([], { x: 0, y: 0 })).toBeUndefined();
  });
});
