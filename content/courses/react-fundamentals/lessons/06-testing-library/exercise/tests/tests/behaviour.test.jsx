import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SearchUsers } from "../src/SearchUsers.jsx";
import { Accordion } from "../src/Accordion.jsx";

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe("SearchUsers", () => {
  it("находит пользователей: ввод в поле, клик по кнопке, список результатов", async () => {
    const user = userEvent.setup();
    const search = vi.fn(() => Promise.resolve(["Аня", "Анатолий"]));
    render(<SearchUsers search={search} />);
    await user.type(screen.getByLabelText("Имя"), "Ан");
    await user.click(screen.getByRole("button", { name: "Найти" }));
    expect(await screen.findAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText("Анатолий")).toBeTruthy();
    expect(search).toHaveBeenCalledWith("Ан");
  });

  it("клавиша Enter в поле отправляет форму", async () => {
    const user = userEvent.setup();
    const search = vi.fn(() => Promise.resolve(["Борис"]));
    render(<SearchUsers search={search} />);
    await user.type(screen.getByLabelText("Имя"), "Бор{enter}");
    expect(await screen.findByText("Борис")).toBeTruthy();
    expect(search).toHaveBeenCalledTimes(1);
  });

  it("пока идёт поиск: статус и заблокированная кнопка", async () => {
    const user = userEvent.setup();
    const pending = deferred();
    render(<SearchUsers search={() => pending.promise} />);
    await user.type(screen.getByLabelText("Имя"), "Ан");
    await user.click(screen.getByRole("button", { name: "Найти" }));
    expect(screen.getByRole("status").textContent).toBe("Поиск...");
    expect(screen.getByRole("button", { name: "Найти" }).disabled).toBe(true);
    pending.resolve(["Аня"]);
    await waitFor(() => expect(screen.queryByRole("status")).toBeNull());
    expect(screen.getByRole("button", { name: "Найти" }).disabled).toBe(false);
  });

  it("если никого нет, пишет «Ничего не найдено» без списка", async () => {
    const user = userEvent.setup();
    render(<SearchUsers search={() => Promise.resolve([])} />);
    await user.type(screen.getByLabelText("Имя"), "яя");
    await user.click(screen.getByRole("button", { name: "Найти" }));
    expect(await screen.findByText("Ничего не найдено")).toBeTruthy();
    expect(screen.queryByRole("list")).toBeNull();
  });

  it("при ошибке показывает сообщение с ролью alert", async () => {
    const user = userEvent.setup();
    render(<SearchUsers search={() => Promise.reject(new Error("сбой"))} />);
    await user.type(screen.getByLabelText("Имя"), "Ан");
    await user.click(screen.getByRole("button", { name: "Найти" }));
    expect((await screen.findByRole("alert")).textContent).toBe("Не удалось выполнить поиск");
  });

  it("обрезает пробелы по краям запроса", async () => {
    const user = userEvent.setup();
    const search = vi.fn(() => Promise.resolve([]));
    render(<SearchUsers search={search} />);
    await user.type(screen.getByLabelText("Имя"), "  Аня ");
    await user.click(screen.getByRole("button", { name: "Найти" }));
    await screen.findByText("Ничего не найдено");
    expect(search).toHaveBeenCalledWith("Аня");
  });

  it("пустой запрос не вызывает search", async () => {
    const user = userEvent.setup();
    const search = vi.fn(() => Promise.resolve([]));
    render(<SearchUsers search={search} />);
    await user.type(screen.getByLabelText("Имя"), "   ");
    await user.click(screen.getByRole("button", { name: "Найти" }));
    expect(search).not.toHaveBeenCalled();
    expect(screen.queryByRole("status")).toBeNull();
  });
});

describe("Accordion", () => {
  it("по умолчанию закрыт: кнопка свёрнута, содержимого нет", () => {
    render(<Accordion title="Доставка">Курьером за день</Accordion>);
    expect(screen.getByRole("button", { name: "Доставка", expanded: false })).toBeTruthy();
    expect(screen.queryByText("Курьером за день")).toBeNull();
  });

  it("клик раскрывает панель, повторный клик сворачивает", async () => {
    const user = userEvent.setup();
    render(<Accordion title="Доставка">Курьером за день</Accordion>);
    await user.click(screen.getByRole("button", { name: "Доставка" }));
    expect(screen.getByRole("button", { name: "Доставка", expanded: true })).toBeTruthy();
    expect(screen.getByText("Курьером за день")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Доставка" }));
    expect(screen.queryByText("Курьером за день")).toBeNull();
  });

  it("defaultOpen открывает секцию сразу", () => {
    render(<Accordion title="Оплата" defaultOpen>Картой</Accordion>);
    expect(screen.getByRole("button", { name: "Оплата", expanded: true })).toBeTruthy();
    expect(screen.getByText("Картой")).toBeTruthy();
  });

  it("работает с клавиатуры: Tab и Enter", async () => {
    const user = userEvent.setup();
    render(<Accordion title="Доставка">Курьером за день</Accordion>);
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Доставка" }));
    await user.keyboard("{Enter}");
    expect(screen.getByText("Курьером за день")).toBeTruthy();
  });

  it("заголовок секции доступен как heading", () => {
    render(<Accordion title="Доставка">Курьером за день</Accordion>);
    expect(screen.getByRole("heading", { name: "Доставка" })).toBeTruthy();
  });
});
