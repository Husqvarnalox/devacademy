import { useState } from "react";
import { SearchBox } from "./SearchBox.jsx";

export function FilterableList({ items }) {
  const [query, setQuery] = useState("");
  // Отфильтрованный список - производное значение, в состоянии его не храним.
  const shown = items.filter((item) => item.toLowerCase().includes(query.trim().toLowerCase()));
  return (
    <div>
      <SearchBox value={query} onChange={setQuery} />
      <p>Найдено: {shown.length}</p>
      {shown.length === 0 ? (
        <p>Ничего не найдено</p>
      ) : (
        <ul>
          {shown.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
