import { formatPrice } from "./format.js";

export function ProductCard({ name, cents, inStock }) {
  return (
    <article>
      <h3>{name}</h3>
      <p>{formatPrice(cents)}</p>
      {inStock ? <button type="button">Купить</button> : <p>Нет в наличии</p>}
    </article>
  );
}
