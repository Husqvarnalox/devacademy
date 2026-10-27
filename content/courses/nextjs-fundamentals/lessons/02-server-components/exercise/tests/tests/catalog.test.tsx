import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";

// Настоящий notFound() бросает внутреннюю ошибку Next.js; в тесте подменяем его узнаваемой ошибкой.
vi.mock("next/navigation", () => ({
  notFound: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

// Позволяет тесту «опустошить» каталог, не меняя файл с данными.
const state = vi.hoisted(() => ({ empty: false }));
vi.mock("../src/lib/products", async (importOriginal) => {
  const real = await importOriginal<typeof import("../src/lib/products")>();
  return { ...real, getProducts: async () => (state.empty ? [] : real.getProducts()) };
});

import { notFound } from "next/navigation";
import ProductsPage from "../src/app/products/page";
import ProductPage, { generateMetadata, generateStaticParams } from "../src/app/products/[id]/page";
import ProductsLayout from "../src/app/products/layout";

beforeEach(() => {
  state.empty = false;
  vi.mocked(notFound).mockClear();
});

function linkTexts(): (string | null)[] {
  return screen.getAllByRole("link").map((link) => link.textContent);
}

describe("ProductsPage", () => {
  it("показывает товары ссылками на страницы товаров", async () => {
    render(await ProductsPage({ searchParams: Promise.resolve({}) }));
    const expected: Record<string, string> = {
      "Чайник": "/products/1",
      "Блокнот": "/products/2",
      "Лампа настольная": "/products/3",
      "Кружка": "/products/4",
    };
    for (const [name, href] of Object.entries(expected)) {
      expect(screen.getByRole("link", { name }).getAttribute("href")).toBe(href);
    }
  });

  it("по умолчанию сортирует товары по названию", async () => {
    render(await ProductsPage({ searchParams: Promise.resolve({}) }));
    expect(linkTexts()).toEqual(["Блокнот", "Кружка", "Лампа настольная", "Чайник"]);
  });

  it("при sort=price сортирует товары по возрастанию цены", async () => {
    render(await ProductsPage({ searchParams: Promise.resolve({ sort: "price" }) }));
    expect(linkTexts()).toEqual(["Блокнот", "Кружка", "Чайник", "Лампа настольная"]);
  });

  it("незнакомое значение sort даёт сортировку по названию", async () => {
    render(await ProductsPage({ searchParams: Promise.resolve({ sort: "abc" }) }));
    expect(linkTexts()).toEqual(["Блокнот", "Кружка", "Лампа настольная", "Чайник"]);
  });

  it("показывает цену рядом с названием", async () => {
    render(await ProductsPage({ searchParams: Promise.resolve({}) }));
    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(4);
    const kettle = items.find((item) => item.textContent?.includes("Чайник"));
    expect(kettle?.textContent).toContain("1490 ₽");
  });

  it("при пустом каталоге показывает сообщение вместо списка", async () => {
    state.empty = true;
    render(await ProductsPage({ searchParams: Promise.resolve({}) }));
    expect(screen.getByText("Товаров пока нет")).toBeTruthy();
    expect(screen.queryByRole("list")).toBeNull();
  });

  it("является асинхронным компонентом и ждёт searchParams", async () => {
    const result = ProductsPage({ searchParams: Promise.resolve({ sort: "price" }) });
    expect(result).toBeInstanceOf(Promise);
    render(await result);
    expect(screen.getAllByRole("link")).toHaveLength(4);
  });
});

describe("ProductPage", () => {
  it("показывает название, описание и цену товара", async () => {
    render(await ProductPage({ params: Promise.resolve({ id: "1" }) }));
    expect(screen.getByRole("heading", { level: 1, name: "Чайник" })).toBeTruthy();
    expect(screen.getByText("Стальной чайник на 1,7 литра.")).toBeTruthy();
    expect(screen.getByText("Цена: 1490 ₽")).toBeTruthy();
  });

  it("для неизвестного id вызывает notFound и прерывает отрисовку", async () => {
    await expect(ProductPage({ params: Promise.resolve({ id: "999" }) })).rejects.toThrow("NEXT_NOT_FOUND");
    expect(notFound).toHaveBeenCalledTimes(1);
  });

  it("для существующего товара не вызывает notFound", async () => {
    await ProductPage({ params: Promise.resolve({ id: "4" }) });
    expect(notFound).not.toHaveBeenCalled();
  });
});

describe("generateMetadata", () => {
  it("возвращает название и описание товара", async () => {
    const metadata = await generateMetadata({ params: Promise.resolve({ id: "3" }) });
    expect(metadata).toEqual({
      title: "Лампа настольная",
      description: "Светодиодная лампа с регулировкой яркости.",
    });
  });

  it("для неизвестного товара возвращает только заголовок «Товар не найден»", async () => {
    const metadata = await generateMetadata({ params: Promise.resolve({ id: "999" }) });
    expect(metadata).toEqual({ title: "Товар не найден" });
  });
});

describe("generateStaticParams", () => {
  it("перечисляет id всех товаров строками", async () => {
    const params = await generateStaticParams();
    expect(params).toEqual([{ id: "1" }, { id: "2" }, { id: "3" }, { id: "4" }]);
  });
});

describe("ProductsLayout", () => {
  it("оборачивает содержимое в раздел «Каталог»", () => {
    render(
      <ProductsLayout>
        <p>Содержимое страницы</p>
      </ProductsLayout>,
    );
    const region = screen.getByRole("region", { name: "Каталог" });
    expect(within(region).getByText("Содержимое страницы")).toBeTruthy();
  });

  it("добавляет ссылки сортировки", () => {
    render(
      <ProductsLayout>
        <p>x</p>
      </ProductsLayout>,
    );
    expect(screen.getByRole("link", { name: "По названию" }).getAttribute("href")).toBe("/products");
    expect(screen.getByRole("link", { name: "По цене" }).getAttribute("href")).toBe("/products?sort=price");
  });
});

describe("Серверные компоненты", () => {
  it("файлы страниц и макета не помечены директивой use client", () => {
    const files = ["products/page.tsx", "products/[id]/page.tsx", "products/layout.tsx"];
    for (const file of files) {
      const source = readFileSync(join(import.meta.dirname, "..", "src", "app", file), "utf8");
      expect(source, file).not.toMatch(/["']use client["']/);
    }
  });
});
