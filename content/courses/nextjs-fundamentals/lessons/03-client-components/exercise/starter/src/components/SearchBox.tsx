import type { FormEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";

// TODO: сделайте компонент клиентским (хуки из next/navigation работают только на клиенте).
export default function SearchBox() {
  // Нужно: начальное значение из useSearchParams().get("q"), состояние поля, обработчик отправки с router.push.
  return (
    <form role="search">
      <input type="search" aria-label="Поиск" />
      <button type="submit">Найти</button>
    </form>
  );
}
