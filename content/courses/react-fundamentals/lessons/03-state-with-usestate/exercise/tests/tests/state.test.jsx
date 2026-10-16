import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Counter } from "../src/Counter.jsx";
import { TodoList } from "../src/TodoList.jsx";
import { LazyCounter } from "../src/LazyCounter.jsx";

function deepFreeze(list) {
  list.forEach((item) => Object.freeze(item));
  return Object.freeze(list);
}

describe("Counter", () => {
  it("начинается с начального значения", () => {
    render(<Counter initial={5} />);
    expect(screen.getByText("Значение: 5")).toBeTruthy();
  });

  it("кнопка Плюс увеличивает, Минус уменьшает", () => {
    render(<Counter />);
    fireEvent.click(screen.getByRole("button", { name: "Плюс" }));
    fireEvent.click(screen.getByRole("button", { name: "Плюс" }));
    expect(screen.getByText("Значение: 2")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Минус" }));
    expect(screen.getByText("Значение: 1")).toBeTruthy();
  });

  it("шаг step учитывается", () => {
    render(<Counter step={10} />);
    fireEvent.click(screen.getByRole("button", { name: "Плюс" }));
    expect(screen.getByText("Значение: 10")).toBeTruthy();
  });

  it("Сброс возвращает начальное значение", () => {
    render(<Counter initial={3} />);
    fireEvent.click(screen.getByRole("button", { name: "Плюс" }));
    fireEvent.click(screen.getByRole("button", { name: "Сброс" }));
    expect(screen.getByText("Значение: 3")).toBeTruthy();
  });

  it("Плюс три прибавляет три шага за один клик (обновляющая функция)", () => {
    render(<Counter step={2} />);
    fireEvent.click(screen.getByRole("button", { name: "Плюс три" }));
    expect(screen.getByText("Значение: 6")).toBeTruthy();
  });

  it("два экземпляра хранят состояние независимо", () => {
    render(
      <>
        <section aria-label="первый"><Counter /></section>
        <section aria-label="второй"><Counter /></section>
      </>,
    );
    const first = screen.getByRole("region", { name: "первый" });
    fireEvent.click(first.querySelector("button"));
    expect(first.textContent).toContain("Значение: 1");
    expect(screen.getByRole("region", { name: "второй" }).textContent).toContain("Значение: 0");
  });
});

describe("TodoList", () => {
  const todos = () => deepFreeze([
    { id: 1, text: "Купить чай", done: false },
    { id: 2, text: "Выучить useState", done: true },
  ]);

  it("показывает дела и число невыполненных", () => {
    render(<TodoList initialTodos={todos()} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText("Осталось: 1")).toBeTruthy();
  });

  it("переключает done у выбранного дела", () => {
    render(<TodoList initialTodos={todos()} />);
    fireEvent.click(screen.getByRole("button", { name: "Готово" }));
    expect(screen.getByText("Осталось: 0")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Готово" })).toBeNull();
    expect(screen.getAllByRole("button", { name: "Вернуть" })).toHaveLength(2);
  });

  it("не изменяет исходные данные (иммутабельное обновление)", () => {
    const initial = todos();
    render(<TodoList initialTodos={initial} />);
    fireEvent.click(screen.getByRole("button", { name: "Вернуть" }));
    expect(initial[1].done).toBe(true);
    expect(screen.getByText("Осталось: 2")).toBeTruthy();
  });
});

describe("LazyCounter", () => {
  it("показывает вычисленное начальное значение", () => {
    render(<LazyCounter compute={() => 40} />);
    expect(screen.getByText("Значение: 40")).toBeTruthy();
  });

  it("compute вызывается один раз, а не на каждом рендере", () => {
    const compute = vi.fn(() => 7);
    render(<LazyCounter compute={compute} />);
    fireEvent.click(screen.getByRole("button", { name: "Плюс" }));
    fireEvent.click(screen.getByRole("button", { name: "Плюс" }));
    expect(screen.getByText("Значение: 9")).toBeTruthy();
    expect(compute).toHaveBeenCalledTimes(1);
  });
});
