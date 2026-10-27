// Учебное хранилище в памяти. В настоящем приложении здесь была бы база данных.
export type Note = {
  id: string;
  title: string;
  tags: string[];
  ownerId: string;
};

const notes = new Map<string, Note>();
let counter = 0;

export function insertNote(data: Omit<Note, "id">): Note {
  counter += 1;
  const note: Note = { id: String(counter), ...data };
  notes.set(note.id, note);
  return note;
}

export function findNote(id: string): Note | undefined {
  return notes.get(id);
}

export function removeNote(id: string): boolean {
  return notes.delete(id);
}

export function listNotes(): Note[] {
  return [...notes.values()];
}

// Нужна тестам: сбрасывает хранилище и нумерацию.
export function resetDb(): void {
  notes.clear();
  counter = 0;
}
