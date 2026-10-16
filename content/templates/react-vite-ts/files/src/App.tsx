import { useState } from "react";

export function App() {
  const [count, setCount] = useState(0);
  return (
    <main>
      <h1>{{name}}</h1>
      <p>Нажатий: {count}</p>
      <button onClick={() => setCount((c) => c + 1)}>Увеличить</button>
    </main>
  );
}
