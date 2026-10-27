// @vitest-environment node
import { readFileSync } from "node:fs";
import { beforeEach, describe, expect, it, vi } from "vitest";

// Порядок вызовов фиксируем общим журналом: redirect должен идти после revalidatePath.
const calls: string[] = [];

vi.mock("next/cache", () => ({
  revalidatePath: vi.fn((path: string) => {
    calls.push(`revalidatePath:${path}`);
  }),
}));

vi.mock("next/navigation", () => ({
  redirect: vi.fn((path: string) => {
    calls.push(`redirect:${path}`);
    throw new Error(`NEXT_REDIRECT:${path}`);
  }),
}));

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { createNote, deleteNote, type FormState } from "../src/app/notes/actions";
import { setCurrentUser } from "../src/lib/auth";
import { findNote, insertNote, listNotes, resetDb } from "../src/lib/db";

const initial: FormState = { ok: true, errors: {}, values: { title: "", tags: "" } };

function form(fields: Record<string, string>): FormData {
  const data = new FormData();
  for (const [name, value] of Object.entries(fields)) data.set(name, value);
  return data;
}

// Вызывает действие и возвращает либо результат, либо адрес перенаправления.
async function submit(fields: Record<string, string>) {
  try {
    const state = await createNote(initial, form(fields));
    return { state, redirectedTo: null as string | null };
  } catch (error) {
    const message = error instanceof Error ? error.message : "";
    if (message.startsWith("NEXT_REDIRECT:")) {
      return { state: null, redirectedTo: message.slice("NEXT_REDIRECT:".length) };
    }
    throw error;
  }
}

beforeEach(() => {
  calls.length = 0;
  vi.clearAllMocks();
  resetDb();
  setCurrentUser({ id: "u1", name: "Аня" });
});

describe("Директива", () => {
  it("actions.ts начинается с директивы use server", () => {
    const source = readFileSync(new URL("../src/app/notes/actions.ts", import.meta.url), "utf8");
    const firstStatement = source
      .replace(/^﻿/, "")
      .replace(/^(\s|\/\/[^\n]*\n|\/\*[\s\S]*?\*\/)*/, "");
    expect(firstStatement).toMatch(/^(["'])use server\1/);
  });
});

describe("createNote", () => {
  it("возвращает ошибку для пустого заголовка и сохраняет введённые значения", async () => {
    const { state } = await submit({ title: "   ", tags: "a, b" });
    expect(state).toEqual({
      ok: false,
      errors: { title: "Введите заголовок" },
      values: { title: "   ", tags: "a, b" },
    });
    expect(listNotes()).toHaveLength(0);
  });

  it("сохраняет заметку, обновляет /notes и перенаправляет на страницу заметки", async () => {
    const { redirectedTo } = await submit({ title: "План на неделю", tags: "работа" });
    expect(redirectedTo).toBe("/notes/1");
    expect(findNote("1")).toEqual({ id: "1", title: "План на неделю", tags: ["работа"], ownerId: "u1" });
    expect(revalidatePath).toHaveBeenCalledWith("/notes");
    expect(calls).toEqual(["revalidatePath:/notes", "redirect:/notes/1"]);
  });

  it("обрезает пробелы вокруг заголовка перед сохранением", async () => {
    await submit({ title: "  Список дел  ", tags: "" });
    expect(findNote("1")?.title).toBe("Список дел");
    expect(findNote("1")?.tags).toEqual([]);
  });

  it("принимает заголовок из 80 символов и отклоняет заголовок из 81", async () => {
    const ok = await submit({ title: "а".repeat(80), tags: "" });
    expect(ok.redirectedTo).toBe("/notes/1");

    const tooLong = await submit({ title: "а".repeat(81), tags: "" });
    expect(tooLong.state?.errors.title).toBe("Заголовок не длиннее 80 символов");
    expect(listNotes()).toHaveLength(1);
  });

  it("нормализует теги: обрезает, приводит к нижнему регистру, убирает повторы и пустые", async () => {
    await submit({ title: "Новости", tags: " Новости, СПОРТ ,новости,,Spорт, ,Cats" });
    expect(findNote("1")?.tags).toEqual(["новости", "спорт", "spорт", "cats"]);
  });

  it("отклоняет больше пяти различных тегов, но считает повторы один раз", async () => {
    const many = await submit({ title: "Теги", tags: "a,b,c,d,e,f" });
    expect(many.state?.errors).toEqual({ tags: "Не больше 5 тегов" });
    expect(many.state?.values.tags).toBe("a,b,c,d,e,f");

    const duplicates = await submit({ title: "Теги", tags: "a,A,b,B,c,d,e,E" });
    expect(duplicates.redirectedTo).toBe("/notes/1");
    expect(findNote("1")?.tags).toEqual(["a", "b", "c", "d", "e"]);
  });

  it("сообщает обе ошибки сразу", async () => {
    const { state } = await submit({ title: "", tags: "1,2,3,4,5,6" });
    expect(state?.ok).toBe(false);
    expect(state?.errors).toEqual({ title: "Введите заголовок", tags: "Не больше 5 тегов" });
  });

  it("при ошибке не обновляет кэш и не перенаправляет", async () => {
    await submit({ title: "", tags: "" });
    expect(revalidatePath).not.toHaveBeenCalled();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("записывает владельцем текущего пользователя", async () => {
    setCurrentUser({ id: "u2", name: "Борис" });
    await submit({ title: "Чужая", tags: "" });
    expect(findNote("1")?.ownerId).toBe("u2");
  });

  it("без входа бросает Unauthorized и ничего не сохраняет", async () => {
    setCurrentUser(null);
    await expect(createNote(initial, form({ title: "Тайно", tags: "" }))).rejects.toThrow("Unauthorized");
    expect(listNotes()).toHaveLength(0);
  });

  it("не падает, если поля нет в форме", async () => {
    const { state } = await submit({});
    expect(state?.errors.title).toBe("Введите заголовок");
    expect(state?.values).toEqual({ title: "", tags: "" });
  });
});

describe("deleteNote", () => {
  it("не удаляет чужую заметку и возвращает ошибку", async () => {
    insertNote({ title: "Чужая", tags: [], ownerId: "u2" });
    const result = await deleteNote("1");
    expect(result).toEqual({ ok: false, error: "Нельзя удалять чужую заметку" });
    expect(findNote("1")).toBeDefined();
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("удаляет свою заметку и обновляет /notes", async () => {
    insertNote({ title: "Моя", tags: [], ownerId: "u1" });
    const result = await deleteNote("1");
    expect(result).toEqual({ ok: true });
    expect(findNote("1")).toBeUndefined();
    expect(revalidatePath).toHaveBeenCalledWith("/notes");
  });

  it("без входа отказывает и не трогает данные", async () => {
    insertNote({ title: "Моя", tags: [], ownerId: "u1" });
    setCurrentUser(null);
    expect(await deleteNote("1")).toEqual({ ok: false, error: "Нужно войти" });
    expect(findNote("1")).toBeDefined();
  });

  it("сообщает, что заметка не найдена", async () => {
    expect(await deleteNote("404")).toEqual({ ok: false, error: "Заметка не найдена" });
    expect(revalidatePath).not.toHaveBeenCalled();
  });
});
