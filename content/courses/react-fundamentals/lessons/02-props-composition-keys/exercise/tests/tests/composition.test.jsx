import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Badge } from "../src/Badge.jsx";
import { Card } from "../src/Card.jsx";
import { NoteList } from "../src/NoteList.jsx";

describe("Badge", () => {
  it("показывает переданный текст", () => {
    render(<Badge text="Новое" />);
    expect(screen.getByText("Новое")).toBeTruthy();
  });

  it("тон по умолчанию neutral", () => {
    render(<Badge text="Новое" />);
    expect(screen.getByText("Новое").getAttribute("data-tone")).toBe("neutral");
  });

  it("заданный тон заменяет значение по умолчанию", () => {
    render(<Badge text="Срочно" tone="danger" />);
    expect(screen.getByText("Срочно").getAttribute("data-tone")).toBe("danger");
  });
});

describe("Card", () => {
  it("заголовок выводится как heading", () => {
    render(<Card title="Заказ">текст</Card>);
    expect(screen.getByRole("heading", { name: "Заказ" })).toBeTruthy();
  });

  it("children выводятся внутри карточки", () => {
    render(
      <Card title="Заказ">
        <p>Первый</p>
        <Badge text="Второй" />
      </Card>,
    );
    expect(screen.getByText("Первый")).toBeTruthy();
    expect(screen.getByText("Второй")).toBeTruthy();
  });

  it("подвал показывается, только если передан", () => {
    const { container, rerender } = render(<Card title="Заказ">текст</Card>);
    expect(container.querySelector("footer")).toBeNull();
    rerender(
      <Card title="Заказ" footer={<button type="button">Оплатить</button>}>
        текст
      </Card>,
    );
    expect(container.querySelector("footer")).not.toBeNull();
    expect(screen.getByRole("button", { name: "Оплатить" })).toBeTruthy();
  });

  it("не требует children", () => {
    const { container } = render(<Card title="Пусто" />);
    expect(container.querySelector("h2").textContent).toBe("Пусто");
  });
});

describe("NoteList", () => {
  const a = { id: "a", title: "Первая" };
  const b = { id: "b", title: "Вторая" };

  it("по элементу списка на заметку", () => {
    render(<NoteList notes={[a, b]} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByLabelText("Первая")).toBeTruthy();
  });

  it("ключи по id: введённый текст остаётся у своей заметки после перестановки", () => {
    const { rerender } = render(<NoteList notes={[a, b]} />);
    fireEvent.change(screen.getByLabelText("Первая"), { target: { value: "черновик" } });
    rerender(<NoteList notes={[b, a]} />);
    expect(screen.getByLabelText("Первая").value).toBe("черновик");
    expect(screen.getByLabelText("Вторая").value).toBe("");
  });

  it("ключи по id: удаление первой заметки не сдвигает текст на соседнюю", () => {
    const { rerender } = render(<NoteList notes={[a, b]} />);
    fireEvent.change(screen.getByLabelText("Вторая"), { target: { value: "важно" } });
    rerender(<NoteList notes={[b]} />);
    expect(screen.getByLabelText("Вторая").value).toBe("важно");
  });
});
