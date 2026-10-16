import { useState } from "react";

export function LazyCounter({ compute }) {
  // Передаём саму функцию: React вызовет её один раз, на первом рендере.
  const [value, setValue] = useState(compute);
  return (
    <div>
      <p>Значение: {value}</p>
      <button type="button" onClick={() => setValue((v) => v + 1)}>Плюс</button>
    </div>
  );
}
