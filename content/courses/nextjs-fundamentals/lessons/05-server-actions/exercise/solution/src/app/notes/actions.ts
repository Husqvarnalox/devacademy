"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { getCurrentUser } from "../../lib/auth";
import { findNote, insertNote, removeNote } from "../../lib/db";

export type FormState = {
  ok: boolean;
  errors: { title?: string; tags?: string };
  values: { title: string; tags: string };
};

export type DeleteResult = { ok: true } | { ok: false; error: string };

function readText(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}

function parseTags(raw: string): string[] {
  const tags = raw
    .split(",")
    .map((tag) => tag.trim().toLowerCase())
    .filter((tag) => tag !== "");
  return [...new Set(tags)];
}

export async function createNote(prevState: FormState, formData: FormData): Promise<FormState> {
  const user = await getCurrentUser();
  if (!user) {
    throw new Error("Unauthorized");
  }

  const values = { title: readText(formData, "title"), tags: readText(formData, "tags") };
  const title = values.title.trim();
  const tags = parseTags(values.tags);

  const errors: FormState["errors"] = {};
  if (title === "") {
    errors.title = "Введите заголовок";
  } else if (title.length > 80) {
    errors.title = "Заголовок не длиннее 80 символов";
  }
  if (tags.length > 5) {
    errors.tags = "Не больше 5 тегов";
  }
  if (errors.title || errors.tags) {
    return { ok: false, errors, values };
  }

  const note = insertNote({ title, tags, ownerId: user.id });
  revalidatePath("/notes");
  redirect(`/notes/${note.id}`);
}

export async function deleteNote(id: string): Promise<DeleteResult> {
  const user = await getCurrentUser();
  if (!user) {
    return { ok: false, error: "Нужно войти" };
  }
  const note = findNote(id);
  if (!note) {
    return { ok: false, error: "Заметка не найдена" };
  }
  if (note.ownerId !== user.id) {
    return { ok: false, error: "Нельзя удалять чужую заметку" };
  }
  removeNote(id);
  revalidatePath("/notes");
  return { ok: true };
}
