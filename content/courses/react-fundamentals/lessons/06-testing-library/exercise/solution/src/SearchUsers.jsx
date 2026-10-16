import { useState } from "react";

export function SearchUsers({ search }) {
  const [query, setQuery] = useState("");
  const [state, setState] = useState({ status: "idle" });

  async function handleSubmit(event) {
    event.preventDefault();
    const clean = query.trim();
    if (clean === "") return;
    setState({ status: "loading" });
    try {
      const names = await search(clean);
      setState({ status: "done", names });
    } catch {
      setState({ status: "error" });
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <label>
        Имя
        <input value={query} onChange={(e) => setQuery(e.target.value)} />
      </label>
      <button type="submit" disabled={state.status === "loading"}>Найти</button>
      {state.status === "loading" && <p role="status">Поиск...</p>}
      {state.status === "error" && <p role="alert">Не удалось выполнить поиск</p>}
      {state.status === "done" &&
        (state.names.length === 0 ? (
          <p>Ничего не найдено</p>
        ) : (
          <ul>
            {state.names.map((name) => (
              <li key={name}>{name}</li>
            ))}
          </ul>
        ))}
    </form>
  );
}
