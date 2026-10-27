import Link from "next/link";
import { getProducts } from "../../lib/products";

export default async function ProductsPage({
  searchParams,
}: {
  searchParams: Promise<{ sort?: string }>;
}) {
  // TODO: дождитесь searchParams и getProducts(); при пустом списке верните <p>Товаров пока нет</p>;
  // иначе отсортируйте товары и выведите <ul> со ссылками на /products/<id> и ценой.
  return <p>TODO</p>;
}
