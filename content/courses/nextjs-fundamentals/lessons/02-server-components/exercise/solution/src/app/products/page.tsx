import Link from "next/link";
import { getProducts } from "../../lib/products";

export default async function ProductsPage({
  searchParams,
}: {
  searchParams: Promise<{ sort?: string }>;
}) {
  const { sort } = await searchParams;
  const products = await getProducts();

  if (products.length === 0) {
    return <p>Товаров пока нет</p>;
  }

  const sorted = [...products].sort(
    sort === "price"
      ? (a, b) => a.price - b.price
      : (a, b) => a.name.localeCompare(b.name, "ru"),
  );

  return (
    <ul>
      {sorted.map((product) => (
        <li key={product.id}>
          <Link href={`/products/${product.id}`}>{product.name}</Link> <span>{product.price} ₽</span>
        </li>
      ))}
    </ul>
  );
}
