import { useState } from "react";

export function TodoList({ initialTodos }) {
  const [todos, setTodos] = useState(initialTodos);

  function toggle(id) {
    setTodos((list) => list.map((todo) => (todo.id === id ? { ...todo, done: !todo.done } : todo)));
  }

  const left = todos.filter((todo) => !todo.done).length;
  return (
    <div>
      <p>Осталось: {left}</p>
      <ul>
        {todos.map((todo) => (
          <li key={todo.id}>
            <span>{todo.text}</span>
            <button type="button" aria-pressed={todo.done} onClick={() => toggle(todo.id)}>
              {todo.done ? "Вернуть" : "Готово"}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
