// Заметки можно переставлять, поэтому ключ - id заметки, а не индекс.
export function NoteList({ notes }) {
  return (
    <ul>
      {notes.map((note) => (
        <li key={note.id}>
          <label>
            {note.title}
            <input defaultValue="" />
          </label>
        </li>
      ))}
    </ul>
  );
}
