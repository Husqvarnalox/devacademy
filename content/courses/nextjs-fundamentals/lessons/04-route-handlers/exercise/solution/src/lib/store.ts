// Хранилище заметок в памяти. Файл не меняйте: тесты сбрасывают его через resetNotes().
// В настоящем приложении здесь была бы база данных.

export interface Note {
  id: string;
  title: string;
  text: string;
}

export interface NoteInput {
  title: string;
  text: string;
}

let notes: Note[] = [];
let nextId = 1;

// Возвращает хранилище к исходным трём заметкам; следующий новый id будет "4".
export function resetNotes(): void {
  notes = [
    { id: "1", title: "Покупки", text: "Чай, хлеб, молоко" },
    { id: "2", title: "Идеи для урока", text: "Показать обработчик маршрута" },
    { id: "3", title: "Чайный список", text: "Улун и пуэр" },
  ];
  nextId = 4;
}

resetNotes();

export function listNotes(): Note[] {
  return notes.map((note) => ({ ...note }));
}

export function getNote(id: string): Note | undefined {
  const found = notes.find((note) => note.id === id);
  return found ? { ...found } : undefined;
}

export function createNote(input: NoteInput): Note {
  const note: Note = { id: String(nextId), title: input.title, text: input.text };
  nextId += 1;
  notes.push(note);
  return { ...note };
}

// Обновляет только переданные поля; для неизвестного id возвращает undefined.
export function updateNote(id: string, patch: Partial<NoteInput>): Note | undefined {
  const found = notes.find((note) => note.id === id);
  if (!found) return undefined;
  if (patch.title !== undefined) found.title = patch.title;
  if (patch.text !== undefined) found.text = patch.text;
  return { ...found };
}

// Возвращает true, если заметка существовала и удалена.
export function deleteNote(id: string): boolean {
  const before = notes.length;
  notes = notes.filter((note) => note.id !== id);
  return notes.length < before;
}
