import { describe, it, expect } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { formatPrice } from "../src/format.js";
import { ProductCard } from "../src/ProductCard.jsx";
import { ProductList } from "../src/ProductList.jsx";

describe("formatPrice", () => {
  it("рубли и копейки", () => {
    expect(formatPrice(1250)).toBe("12.50 ₽");
    expect(formatPrice(700)).toBe("7.00 ₽");
  });
  it("копейки всегда двузначные", () => {
    expect(formatPrice(5)).toBe("0.05 ₽");
    expect(formatPrice(0)).toBe("0.00 ₽");
    expect(formatPrice(100005)).toBe("1000.05 ₽");
  });
});

describe("ProductCard", () => {
  it("показывает название заголовком", () => {
    render(<ProductCard name="Чай" cents={1250} inStock />);
    expect(screen.getByRole("heading", { name: "Чай" })).toBeTruthy();
  });

  it("показывает цену", () => {
    render(<ProductCard name="Чай" cents={1250} inStock />);
    expect(screen.getByText("12.50 ₽")).toBeTruthy();
  });

  it("в наличии: есть кнопка «Купить»", () => {
    render(<ProductCard name="Чай" cents={1250} inStock />);
    expect(screen.getByRole("button", { name: "Купить" })).toBeTruthy();
    expect(screen.queryByText("Нет в наличии")).toBeNull();
  });

  it("нет в наличии: вместо кнопки надпись", () => {
    render(<ProductCard name="Кофе" cents={900} inStock={false} />);
    expect(screen.getByText("Нет в наличии")).toBeTruthy();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("результат зависит только от props: новые props дают новый вывод", () => {
    const { rerender } = render(<ProductCard name="Чай" cents={1250} inStock />);
    rerender(<ProductCard name="Кофе" cents={5} inStock={false} />);
    expect(screen.queryByRole("heading", { name: "Чай" })).toBeNull();
    expect(screen.getByRole("heading", { name: "Кофе" })).toBeTruthy();
    expect(screen.getByText("0.05 ₽")).toBeTruthy();
  });

  it("не изменяет переданные props", () => {
    const props = Object.freeze({ name: "Чай", cents: 1250, inStock: true });
    render(<ProductCard {...props} />);
    expect(props).toEqual({ name: "Чай", cents: 1250, inStock: true });
  });
});

describe("ProductList", () => {
  const products = [
    { id: 1, name: "Чай", cents: 1250, inStock: true },
    { id: 2, name: "Кофе", cents: 900, inStock: false },
    { id: 3, name: "Какао", cents: 5, inStock: true },
  ];

  it("рисует по элементу списка на товар", () => {
    render(<ProductList products={products} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
  });

  it("внутри элементов лежат карточки с названиями", () => {
    render(<ProductList products={products} />);
    const items = screen.getAllByRole("listitem");
    expect(within(items[1]).getByRole("heading", { name: "Кофе" })).toBeTruthy();
    expect(within(items[1]).getByText("Нет в наличии")).toBeTruthy();
    expect(within(items[2]).getByText("0.05 ₽")).toBeTruthy();
  });

  it("пустой список: абзац вместо списка", () => {
    render(<ProductList products={[]} />);
    expect(screen.getByText("Список пуст")).toBeTruthy();
    expect(screen.queryByRole("list")).toBeNull();
  });
});
