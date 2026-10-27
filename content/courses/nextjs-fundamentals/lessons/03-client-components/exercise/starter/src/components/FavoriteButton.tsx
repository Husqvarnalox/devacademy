// TODO: сделайте компонент клиентским и добавьте состояние «в избранном».
export default function FavoriteButton({ title, initial = false }: { title: string; initial?: boolean }) {
  // Кнопка: type="button", aria-pressed (true/false), aria-label вида "<title>: В избранное" / "<title>: В избранном",
  // видимый текст "В избранное" / "В избранном"; по клику состояние переключается.
  return <button type="button">TODO</button>;
}
