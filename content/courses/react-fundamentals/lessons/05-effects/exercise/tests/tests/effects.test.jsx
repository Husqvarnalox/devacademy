import { StrictMode } from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { UserProfile } from "../src/UserProfile.jsx";
import { TitleSync } from "../src/TitleSync.jsx";
import { OnlineBadge } from "../src/OnlineBadge.jsx";

// Управляемый промис: тест сам решает, когда ответ придёт. Сети нет.
function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe("UserProfile", () => {
  it("сначала показывает загрузку, затем имя пользователя", async () => {
    const fetchUser = vi.fn(() => Promise.resolve({ name: "Аня" }));
    render(<UserProfile userId={1} fetchUser={fetchUser} />);
    expect(screen.getByText("Загрузка...")).toBeTruthy();
    expect(await screen.findByRole("heading", { name: "Аня" })).toBeTruthy();
    expect(screen.queryByText("Загрузка...")).toBeNull();
  });

  it("вызывает fetchUser с id и сигналом отмены", async () => {
    const fetchUser = vi.fn(() => Promise.resolve({ name: "Аня" }));
    render(<UserProfile userId={42} fetchUser={fetchUser} />);
    await screen.findByRole("heading");
    expect(fetchUser).toHaveBeenCalledTimes(1);
    expect(fetchUser.mock.calls[0][0]).toBe(42);
    expect(fetchUser.mock.calls[0][1]).toBeInstanceOf(AbortSignal);
  });

  it("при ошибке показывает сообщение", async () => {
    const fetchUser = vi.fn(() => Promise.reject(new Error("сервер недоступен")));
    render(<UserProfile userId={1} fetchUser={fetchUser} />);
    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toBe("Ошибка: сервер недоступен");
  });

  it("при смене userId отменяет прежний запрос и запрашивает нового пользователя", async () => {
    const fetchUser = vi.fn((id) => Promise.resolve({ name: `Пользователь ${id}` }));
    const { rerender } = render(<UserProfile userId={1} fetchUser={fetchUser} />);
    await screen.findByRole("heading", { name: "Пользователь 1" });
    rerender(<UserProfile userId={2} fetchUser={fetchUser} />);
    expect(fetchUser.mock.calls[0][1].aborted).toBe(true);
    expect(await screen.findByRole("heading", { name: "Пользователь 2" })).toBeTruthy();
    expect(fetchUser).toHaveBeenCalledTimes(2);
  });

  it("устаревший ответ не затирает свежий", async () => {
    const first = deferred();
    const second = deferred();
    const fetchUser = vi.fn((id) => (id === 1 ? first.promise : second.promise));
    const { rerender } = render(<UserProfile userId={1} fetchUser={fetchUser} />);
    rerender(<UserProfile userId={2} fetchUser={fetchUser} />);
    second.resolve({ name: "Борис" });
    expect(await screen.findByRole("heading", { name: "Борис" })).toBeTruthy();
    first.resolve({ name: "Аня" }); // опоздавший ответ на прежний запрос
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.getByRole("heading").textContent).toBe("Борис");
    expect(screen.queryByText("Аня")).toBeNull();
  });

  it("в StrictMode первый запрос отменяется, второй доходит до экрана", async () => {
    const fetchUser = vi.fn(() => Promise.resolve({ name: "Аня" }));
    render(
      <StrictMode>
        <UserProfile userId={1} fetchUser={fetchUser} />
      </StrictMode>,
    );
    expect(await screen.findByRole("heading", { name: "Аня" })).toBeTruthy();
    expect(fetchUser).toHaveBeenCalledTimes(2);
    expect(fetchUser.mock.calls[0][1].aborted).toBe(true);
    expect(fetchUser.mock.calls[1][1].aborted).toBe(false);
  });

  it("при размонтировании отменяет запрос", async () => {
    const pending = deferred();
    const fetchUser = vi.fn(() => pending.promise);
    const { unmount } = render(<UserProfile userId={1} fetchUser={fetchUser} />);
    unmount();
    expect(fetchUser.mock.calls[0][1].aborted).toBe(true);
    pending.resolve({ name: "Аня" });
    await new Promise((r) => setTimeout(r, 0));
  });
});

describe("TitleSync", () => {
  it("записывает title в document.title", () => {
    document.title = "Исходный";
    render(<TitleSync title="Заказы" />);
    expect(document.title).toBe("Заказы");
  });

  it("обновляет заголовок при смене prop", () => {
    document.title = "Исходный";
    const { rerender } = render(<TitleSync title="Заказы" />);
    rerender(<TitleSync title="Товары" />);
    expect(document.title).toBe("Товары");
  });

  it("при размонтировании возвращает прежний заголовок", () => {
    document.title = "Исходный";
    const { unmount } = render(<TitleSync title="Заказы" />);
    unmount();
    expect(document.title).toBe("Исходный");
  });
});

describe("OnlineBadge", () => {
  it("показывает текущее состояние сети", () => {
    render(<OnlineBadge />);
    expect(screen.getByText("В сети")).toBeTruthy();
  });

  it("реагирует на события offline и online", () => {
    render(<OnlineBadge />);
    fireEvent(window, new Event("offline"));
    expect(screen.getByText("Нет сети")).toBeTruthy();
    fireEvent(window, new Event("online"));
    expect(screen.getByText("В сети")).toBeTruthy();
  });

  it("при размонтировании снимает те же обработчики, что подписал", () => {
    const names = ["online", "offline"];
    const add = vi.spyOn(window, "addEventListener");
    const remove = vi.spyOn(window, "removeEventListener");
    try {
      const { unmount } = render(<OnlineBadge />);
      const added = add.mock.calls.filter((c) => names.includes(c[0]));
      unmount();
      const removed = remove.mock.calls.filter((c) => names.includes(c[0]));
      expect(added).toHaveLength(2);
      expect(removed).toHaveLength(2);
      for (const [type, handler] of added) {
        expect(removed.some((r) => r[0] === type && r[1] === handler)).toBe(true);
      }
    } finally {
      add.mockRestore();
      remove.mockRestore();
    }
  });
});
