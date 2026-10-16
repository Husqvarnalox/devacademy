export function SearchBox({ value, onChange }) {
  return (
    <label>
      Поиск
      <input value={value} onChange={(e) => onChange(e.target.value)} />
    </label>
  );
}
