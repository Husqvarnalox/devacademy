import { readFileSync } from "node:fs";
import { join } from "node:path";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));
vi.mock("next/navigation", () => ({
  redirect: vi.fn((path: string) => {
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));

// Настоящее действие остаётся, но обёрнуто в vi.fn: в нужных тестах поведение подменяется.
vi.mock("../src/app/notes/actions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../src/app/notes/actions")>();
  return { ...actual, createNote: vi.fn(actual.createNote) };
});

import { createNote } from "../src/app/notes/actions";
import { NoteForm } from "../src/app/notes/NoteForm";
import { setCurrentUser } from "../src/lib/auth";
import { resetDb } from "../src/lib/db";

beforeEach(() => {
  resetDb();
  setCurrentUser({ id: "u1", name: "Аня" });
});

function fill(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

function submitForm() {
  fireEvent.submit(screen.getByRole("button", { name: /Сохранить|Сохранение/ }).closest("form")!);
}

describe("Директива клиента", () => {
  it("NoteForm.tsx начинается с директивы use client", () => {
    const source = readFileSync(join(import.meta.dirname, "../src/app/notes/NoteForm.tsx"), "utf8");
    const firstStatement = source
      .replace(/^﻿/, "")
      .replace(/^(\s|\/\/[^\n]*\n|\/\*[\s\S]*?\*\/)*/, "");
    expect(firstStatement).toMatch(/^(["'])use client\1/);
  });
});

describe("NoteForm", () => {
  it("показывает ошибку заголовка в элементе с role=alert", async () => {
    render(<NoteForm />);
    expect(screen.queryByRole("alert")).toBeNull();
    fill("Заголовок", "   ");
    submitForm();
    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toBe("Введите заголовок");
  });

  it("после ошибки сохраняет введённые значения в полях", async () => {
    render(<NoteForm />);
    fill("Теги", "Реакт, Next");
    submitForm();
    await screen.findByRole("alert");
    expect((screen.getByLabelText("Теги") as HTMLInputElement).value).toBe("Реакт, Next");
    expect((screen.getByLabelText("Заголовок") as HTMLInputElement).value).toBe("");
  });

  it("показывает ошибки заголовка и тегов в отдельных alert", async () => {
    render(<NoteForm />);
    fill("Теги", "1,2,3,4,5,6");
    submitForm();
    await waitFor(() => expect(screen.getAllByRole("alert")).toHaveLength(2));
    const texts = screen.getAllByRole("alert").map((node) => node.textContent);
    expect(texts).toEqual(["Введите заголовок", "Не больше 5 тегов"]);
  });

  it("ошибка исчезает после исправления и повторной отправки", async () => {
    render(<NoteForm />);
    submitForm();
    await screen.findByRole("alert");
    fill("Заголовок", "Исправлено");
    // Успешная отправка заканчивается перенаправлением; здесь действие подменено, чтобы не бросать ошибку.
    vi.mocked(createNote).mockImplementationOnce(async (_prev, data) => ({
      ok: true,
      errors: {},
      values: { title: String(data.get("title")), tags: "" },
    }));
    submitForm();
    await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
  });

  it("пока действие выполняется, кнопка отключена и показывает «Сохранение...»", async () => {
    let finish!: () => void;
    vi.mocked(createNote).mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = () => resolve({ ok: true, errors: {}, values: { title: "", tags: "" } });
        }),
    );
    render(<NoteForm />);
    const button = screen.getByRole("button", { name: "Сохранить" }) as HTMLButtonElement;
    expect(button.disabled).toBe(false);

    fill("Заголовок", "Привет");
    submitForm();
    const pending = (await screen.findByRole("button", { name: "Сохранение..." })) as HTMLButtonElement;
    expect(pending.disabled).toBe(true);

    await act(async () => finish());
    const done = (await screen.findByRole("button", { name: "Сохранить" })) as HTMLButtonElement;
    expect(done.disabled).toBe(false);
  });

  it("передаёт действию FormData с полями title и tags", async () => {
    vi.mocked(createNote).mockImplementationOnce(async (_prev, data) => ({
      ok: false,
      errors: { title: "проверка" },
      values: { title: String(data.get("title")), tags: String(data.get("tags")) },
    }));
    render(<NoteForm />);
    fill("Заголовок", "Привет");
    fill("Теги", "а, б");
    submitForm();
    await screen.findByRole("alert");
    expect(vi.mocked(createNote).mock.calls[0][1].get("title")).toBe("Привет");
    expect(vi.mocked(createNote).mock.calls[0][1].get("tags")).toBe("а, б");
  });
});
