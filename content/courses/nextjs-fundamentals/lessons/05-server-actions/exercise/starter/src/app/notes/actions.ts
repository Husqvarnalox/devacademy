// TODO: сделайте этот файл файлом серверных действий (директива первой строкой).

import { getCurrentUser } from "../../lib/auth";
import { findNote, insertNote, removeNote } from "../../lib/db";

export type FormState = {
  ok: boolean;
  errors: { title?: string; tags?: string };
  values: { title: string; tags: string };
};

export type DeleteResult = { ok: true } | { ok: false; error: string };

export async function createNote(prevState: FormState, formData: FormData): Promise<FormState> {
  // TODO: проверьте пользователя, разберите и проверьте поля title и tags,
  // при ошибке верните { ok: false, errors, values }; иначе сохраните заметку
  // (insertNote), обновите кэш /notes и перенаправьте на /notes/<id>.
  void getCurrentUser;
  void insertNote;
  return prevState;
}

export async function deleteNote(id: string): Promise<DeleteResult> {
  // TODO: удалять можно только свою заметку (findNote, removeNote, getCurrentUser).
  void findNote;
  void removeNote;
  return { ok: true };
}
