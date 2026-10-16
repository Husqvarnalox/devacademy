import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SignupForm } from "../src/SignupForm.jsx";
import { SearchBox } from "../src/SearchBox.jsx";
import { FilterableList } from "../src/FilterableList.jsx";

function type(label, text) {
  fireEvent.change(screen.getByLabelText(label), { target: { value: text } });
}

describe("SignupForm", () => {
  it("поля управляемые: значение берётся из состояния", () => {
    render(<SignupForm onSubmit={() => {}} />);
    type("Имя", "Аня");
    type("Email", "anya@example.com");
    expect(screen.getByLabelText("Имя").value).toBe("Аня");
    expect(screen.getByLabelText("Email").value).toBe("anya@example.com");
  });

  it("отправка передаёт имя и email в onSubmit", () => {
    const onSubmit = vi.fn();
    render(<SignupForm onSubmit={onSubmit} />);
    type("Имя", "Аня");
    type("Email", "anya@example.com");
    fireEvent.click(screen.getByRole("button", { name: "Отправить" }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit).toHaveBeenCalledWith({ name: "Аня", email: "anya@example.com" });
  });

  it("пустое имя: ошибка и onSubmit не вызывается", () => {
    const onSubmit = vi.fn();
    render(<SignupForm onSubmit={onSubmit} />);
    type("Имя", "   ");
    type("Email", "anya@example.com");
    fireEvent.click(screen.getByRole("button", { name: "Отправить" }));
    expect(screen.getByRole("alert").textContent).toBe("Введите имя");
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("email без @: ошибка", () => {
    const onSubmit = vi.fn();
    render(<SignupForm onSubmit={onSubmit} />);
    type("Имя", "Аня");
    type("Email", "anya");
    fireEvent.click(screen.getByRole("button", { name: "Отправить" }));
    expect(screen.getByRole("alert").textContent).toBe("Некорректный email");
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("обрезает пробелы по краям", () => {
    const onSubmit = vi.fn();
    render(<SignupForm onSubmit={onSubmit} />);
    type("Имя", "  Аня ");
    type("Email", " anya@example.com ");
    fireEvent.click(screen.getByRole("button", { name: "Отправить" }));
    expect(onSubmit).toHaveBeenCalledWith({ name: "Аня", email: "anya@example.com" });
  });

  it("отменяет стандартную отправку формы (preventDefault)", () => {
    const { container } = render(<SignupForm onSubmit={() => {}} />);
    type("Имя", "Аня");
    type("Email", "anya@example.com");
    // fireEvent возвращает false, если обработчик вызвал preventDefault
    const notPrevented = fireEvent.submit(container.querySelector("form"));
    expect(notPrevented).toBe(false);
  });

  it("после успешной отправки поля очищаются, ошибка пропадает", () => {
    render(<SignupForm onSubmit={() => {}} />);
    fireEvent.click(screen.getByRole("button", { name: "Отправить" }));
    expect(screen.getByRole("alert")).toBeTruthy();
    type("Имя", "Аня");
    type("Email", "anya@example.com");
    fireEvent.click(screen.getByRole("button", { name: "Отправить" }));
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.getByLabelText("Имя").value).toBe("");
    expect(screen.getByLabelText("Email").value).toBe("");
  });
});

describe("SearchBox", () => {
  it("показывает value и сообщает о вводе через onChange", () => {
    const onChange = vi.fn();
    render(<SearchBox value="чай" onChange={onChange} />);
    expect(screen.getByLabelText("Поиск").value).toBe("чай");
    fireEvent.change(screen.getByLabelText("Поиск"), { target: { value: "кофе" } });
    expect(onChange).toHaveBeenCalledWith("кофе");
  });

  it("сам ничего не хранит: без обновления value поле возвращается к значению props", () => {
    render(<SearchBox value="чай" onChange={() => {}} />);
    fireEvent.change(screen.getByLabelText("Поиск"), { target: { value: "кофе" } });
    expect(screen.getByLabelText("Поиск").value).toBe("чай");
  });
});

describe("FilterableList", () => {
  const items = ["Чай чёрный", "Чай зелёный", "Кофе"];

  it("показывает все элементы и их число", () => {
    render(<FilterableList items={items} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
    expect(screen.getByText("Найдено: 3")).toBeTruthy();
  });

  it("фильтрует по вводу без учёта регистра", () => {
    render(<FilterableList items={items} />);
    type("Поиск", "ЧАЙ");
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText("Найдено: 2")).toBeTruthy();
  });

  it("если ничего не найдено, выводит сообщение вместо списка", () => {
    render(<FilterableList items={items} />);
    type("Поиск", "сок");
    expect(screen.getByText("Ничего не найдено")).toBeTruthy();
    expect(screen.queryByRole("list")).toBeNull();
    expect(screen.getByText("Найдено: 0")).toBeTruthy();
  });
});
