// @vitest-environment node
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { beforeEach, describe, expect, it, vi } from "vitest";

// Vitest не умеет кэшировать: у него нет компилятора Next.js, и "use cache" здесь - обычная строка.
// Поэтому проверяем две вещи: что код объявляет кэш правильно (директива, теги, срок жизни)
// и что действия сбрасывают нужные теги. Сами вызовы next/cache записываются макетами.
vi.mock("next/cache", () => ({
  cacheTag: vi.fn(),
  cacheLife: vi.fn(),
  updateTag: vi.fn(),
  revalidateTag: vi.fn(),
  revalidatePath: vi.fn(),
}));

import { cacheLife, cacheTag, revalidatePath, revalidateTag, updateTag } from "next/cache";
import { importCatalog, updatePrice } from "../src/app/admin/actions";
import { getPrices, getProduct, getProducts } from "../src/lib/catalog";
import { calls, fetchProduct, resetSource } from "../src/lib/source";

function readSource(path: string): string {
  return readFileSync(join(import.meta.dirname, "..", path), "utf8");
}

// Тело функции начинается (после пробелов и комментариев) со строки-директивы.
function startsWithDirective(source: string, functionName: string, directive: string): boolean {
  const pattern = new RegExp(
    `export\\s+async\\s+function\\s+${functionName}\\s*\\([^)]*\\)[^{]*\\{(?:\\s|//[^\\n]*\\n|/\\*[\\s\\S]*?\\*/)*(["'])${directive}\\1`,
  );
  return pattern.test(source);
}

function fileStartsWith(source: string, directive: string): boolean {
  const body = source.replace(/^﻿/, "").replace(/^(\s|\/\/[^\n]*\n|\/\*[\s\S]*?\*\/)*/, "");
  return new RegExp(`^(["'])${directive}\\1`).test(body);
}

function tagsOf(): string[] {
  return vi.mocked(cacheTag).mock.calls.flat().sort();
}

beforeEach(() => {
  vi.clearAllMocks();
  resetSource();
});

describe("Директивы", () => {
  it("каждая функция каталога начинается с use cache", () => {
    const source = readSource("src/lib/catalog.ts");
    for (const name of ["getProducts", "getProduct", "getPrices"]) {
      expect(startsWithDirective(source, name, "use cache"), `${name}: "use cache" должен быть первым оператором тела`).toBe(true);
    }
  });

  it("admin/actions.ts начинается с директивы use server", () => {
    expect(fileStartsWith(readSource("src/app/admin/actions.ts"), "use server")).toBe(true);
  });

  it("каталог не читает cookies и headers внутри кэшируемых функций", () => {
    const code = readSource("src/lib/catalog.ts").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/[^\n]*/g, "");
    expect(code).not.toMatch(/next\/headers/);
    expect(code).not.toMatch(/\bcookies\s*\(/);
    expect(code).not.toMatch(/\bheaders\s*\(/);
  });
});

describe("getProducts", () => {
  it("помечает кэш тегом products и временем жизни hours", async () => {
    const products = await getProducts();
    expect(products).toHaveLength(3);
    expect(tagsOf()).toEqual(["products"]);
    expect(cacheLife).toHaveBeenCalledTimes(1);
    expect(cacheLife).toHaveBeenCalledWith("hours");
  });
});

describe("getProduct", () => {
  it("помечает кэш тегами products и product-<id>", async () => {
    const product = await getProduct("p2");
    expect(product).toEqual({ id: "p2", name: "Чайник", price: 2490.5 });
    expect(tagsOf()).toEqual(["product-p2", "products"]);
    expect(cacheLife).toHaveBeenCalledTimes(1);
    expect(cacheLife).toHaveBeenCalledWith("hours");
  });

  it("для разных товаров ставит разные теги", async () => {
    await getProduct("p1");
    const first = tagsOf();
    vi.mocked(cacheTag).mockClear();
    await getProduct("p3");
    expect(first).toEqual(["product-p1", "products"]);
    expect(tagsOf()).toEqual(["product-p3", "products"]);
  });

  it("возвращает null для неизвестного товара", async () => {
    expect(await getProduct("нет-такого")).toBeNull();
    expect(calls.fetchProduct).toBe(1);
  });
});

describe("getPrices", () => {
  it("принимает валюту аргументом и передаёт её источнику", async () => {
    const usd = await getPrices("USD");
    expect(usd.p1).toBe(38.5);
    const eur = await getPrices("EUR");
    expect(eur.p1).toBe(35);
    expect(calls.fetchPrices).toBe(2);
  });

  it("объявляет срок жизни hours и тег products", async () => {
    await getPrices("RUB");
    expect(cacheLife).toHaveBeenCalledWith("hours");
    expect(tagsOf()).toEqual(["products"]);
  });

  it("не скрывает ошибку источника для неизвестной валюты", async () => {
    await expect(getPrices("XXX")).rejects.toThrow("Неизвестная валюта: XXX");
  });
});

describe("updatePrice", () => {
  it("сохраняет цену, обновляет тег товара и помечает products устаревшим", async () => {
    const result = await updatePrice("p1", 3999.99);
    expect(result).toEqual({ ok: true });
    expect((await fetchProduct("p1"))?.price).toBe(3999.99);
    expect(updateTag).toHaveBeenCalledTimes(1);
    expect(updateTag).toHaveBeenCalledWith("product-p1");
    expect(revalidateTag).toHaveBeenCalledTimes(1);
    expect(revalidateTag).toHaveBeenCalledWith("products", "max");
  });

  it("отклоняет нулевую, отрицательную и нечисловую цену", async () => {
    for (const bad of [0, -5, Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY]) {
      const result = await updatePrice("p1", bad);
      expect(result, `цена ${bad}`).toEqual({ ok: false, error: "Цена должна быть числом больше нуля" });
    }
  });

  it("принимает цену ровно 1000000 и отклоняет большую", async () => {
    expect(await updatePrice("p1", 1_000_000)).toEqual({ ok: true });
    expect(await updatePrice("p1", 1_000_000.01)).toEqual({ ok: false, error: "Цена не может быть больше 1000000" });
    expect((await fetchProduct("p1"))?.price).toBe(1_000_000);
  });

  it("принимает два знака после запятой и отклоняет больше", async () => {
    expect(await updatePrice("p1", 0.01)).toEqual({ ok: true });
    expect(await updatePrice("p1", 19.99)).toEqual({ ok: true });
    expect(await updatePrice("p1", 19.999)).toEqual({ ok: false, error: "Не больше двух знаков после запятой" });
    expect(await updatePrice("p1", 0.001)).toEqual({ ok: false, error: "Не больше двух знаков после запятой" });
  });

  it("при неверной цене ничего не сохраняет и не сбрасывает кэш", async () => {
    await updatePrice("p1", -1);
    await updatePrice("p1", 2000000);
    await updatePrice("p1", 1.234);
    expect(calls.savePrice).toBe(0);
    expect(updateTag).not.toHaveBeenCalled();
    expect(revalidateTag).not.toHaveBeenCalled();
  });

  it("для неизвестного товара возвращает ошибку без сброса кэша", async () => {
    expect(await updatePrice("p404", 100)).toEqual({ ok: false, error: "Товар не найден" });
    expect(updateTag).not.toHaveBeenCalled();
    expect(revalidateTag).not.toHaveBeenCalled();
  });

  it("не вызывает revalidateTag с одним аргументом", async () => {
    await updatePrice("p3", 1000);
    await importCatalog();
    expect(vi.mocked(revalidateTag).mock.calls.length).toBeGreaterThan(0);
    for (const call of vi.mocked(revalidateTag).mock.calls) {
      expect(call).toHaveLength(2);
      expect(call[1]).toBe("max");
    }
  });
});

describe("importCatalog", () => {
  it("помечает products устаревшим и больше ничего не сбрасывает", async () => {
    const result = await importCatalog();
    expect(result).toEqual({ ok: true });
    expect(calls.importAll).toBe(1);
    expect(revalidateTag).toHaveBeenCalledTimes(1);
    expect(revalidateTag).toHaveBeenCalledWith("products", "max");
    expect(updateTag).not.toHaveBeenCalled();
    expect(revalidatePath).not.toHaveBeenCalled();
  });
});
