// TODO: превратите форму в клиентский компонент и подключите действие createNote через useActionState.
export function NoteForm() {
  return (
    <form>
      <label>
        Заголовок
        <input name="title" />
      </label>

      <label>
        Теги
        <input name="tags" />
      </label>

      <button type="submit">Сохранить</button>
    </form>
  );
}
