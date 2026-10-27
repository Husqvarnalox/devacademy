"use client";

import { useActionState } from "react";
import { createNote, type FormState } from "./actions";

const initialState: FormState = {
  ok: true,
  errors: {},
  values: { title: "", tags: "" },
};

export function NoteForm() {
  const [state, formAction, pending] = useActionState(createNote, initialState);

  return (
    <form action={formAction}>
      <label>
        Заголовок
        <input name="title" defaultValue={state.values.title} />
      </label>
      {state.errors.title && <p role="alert">{state.errors.title}</p>}

      <label>
        Теги
        <input name="tags" defaultValue={state.values.tags} />
      </label>
      {state.errors.tags && <p role="alert">{state.errors.tags}</p>}

      <button type="submit" disabled={pending}>
        {pending ? "Сохранение..." : "Сохранить"}
      </button>
    </form>
  );
}
