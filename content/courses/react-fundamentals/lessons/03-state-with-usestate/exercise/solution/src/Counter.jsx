import { useState } from "react";

export function Counter({ initial = 0, step = 1 }) {
  const [n, setN] = useState(initial);
  return (
    <div>
      <p>Значение: {n}</p>
      <button type="button" onClick={() => setN((v) => v + step)}>Плюс</button>
      <button type="button" onClick={() => setN((v) => v - step)}>Минус</button>
      <button
        type="button"
        onClick={() => {
          // Обновляющая функция получает актуальное значение: три вызова дают +3 шага.
          setN((v) => v + step);
          setN((v) => v + step);
          setN((v) => v + step);
        }}
      >
        Плюс три
      </button>
      <button type="button" onClick={() => setN(initial)}>Сброс</button>
    </div>
  );
}
