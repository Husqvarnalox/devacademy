import { describe, it, expect } from "vitest";
import { reducer, isBusy, summary, type State } from "../src/state.ts";

const idle: State = { status: "idle" };
const loading: State = { status: "loading", startedAt: 10, retries: 0 };
const failed: State = { status: "error", message: "сеть", retries: 2 };
const done: State = { status: "success", data: ["a", "b"], loadedAt: 20 };

describe("reducer", () => {
  it("fetch из idle, success и error запускает загрузку", () => {
    expect(reducer(idle, { type: "fetch", at: 1 })).toEqual({ status: "loading", startedAt: 1, retries: 0 });
    expect(reducer(done, { type: "fetch", at: 2 })).toEqual({ status: "loading", startedAt: 2, retries: 0 });
    expect(reducer(failed, { type: "fetch", at: 3 })).toEqual({ status: "loading", startedAt: 3, retries: 2 });
  });
  it("fetch во время загрузки ничего не меняет (тот же объект)", () => {
    expect(reducer(loading, { type: "fetch", at: 99 })).toBe(loading);
  });
  it("resolve завершает загрузку", () => {
    expect(reducer(loading, { type: "resolve", data: ["x"], at: 30 })).toEqual({ status: "success", data: ["x"], loadedAt: 30 });
  });
  it("resolve вне загрузки игнорируется", () => {
    expect(reducer(idle, { type: "resolve", data: [], at: 1 })).toBe(idle);
    expect(reducer(done, { type: "resolve", data: [], at: 1 })).toBe(done);
  });
  it("reject увеличивает retries", () => {
    expect(reducer(loading, { type: "reject", message: "500" })).toEqual({ status: "error", message: "500", retries: 1 });
    const again = reducer({ status: "loading", startedAt: 5, retries: 2 }, { type: "reject", message: "снова" });
    expect(again).toEqual({ status: "error", message: "снова", retries: 3 });
  });
  it("reject вне загрузки игнорируется", () => {
    expect(reducer(idle, { type: "reject", message: "x" })).toBe(idle);
    expect(reducer(failed, { type: "reject", message: "x" })).toBe(failed);
  });
  it("reset возвращает idle из любого состояния", () => {
    for (const st of [idle, loading, done, failed]) expect(reducer(st, { type: "reset" })).toEqual({ status: "idle" });
  });
  it("данные копируются, чтобы изменение исходного массива не влияло", () => {
    const data = ["a"];
    const r = reducer(loading, { type: "resolve", data, at: 1 });
    data.push("b");
    expect(r).toEqual({ status: "success", data: ["a"], loadedAt: 1 });
  });
});

describe("isBusy и summary", () => {
  it("isBusy только при загрузке", () => {
    expect([idle, loading, done, failed].map(isBusy)).toEqual([false, true, false, false]);
  });
  it("summary для каждого состояния", () => {
    expect(summary(idle)).toBe("ожидание");
    expect(summary(loading)).toBe("загрузка с 10");
    expect(summary(done)).toBe("готово: 2 эл.");
    expect(summary(failed)).toBe("ошибка (попытка 2): сеть");
  });
  it("пустой результат - это тоже успех", () => {
    expect(summary({ status: "success", data: [], loadedAt: 0 })).toBe("готово: 0 эл.");
    expect(summary({ status: "loading", startedAt: 0, retries: 0 })).toBe("загрузка с 0");
  });
});
