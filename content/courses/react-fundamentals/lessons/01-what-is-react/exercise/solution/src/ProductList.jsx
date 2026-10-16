import { ProductCard } from "./ProductCard.jsx";

export function ProductList({ products }) {
  if (products.length === 0) return <p>Список пуст</p>;
  return (
    <ul>
      {products.map((product) => (
        <li key={product.id}>
          <ProductCard name={product.name} cents={product.cents} inStock={product.inStock} />
        </li>
      ))}
    </ul>
  );
}
