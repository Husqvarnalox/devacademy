import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getProduct, getProducts } from "../../../lib/products";

type Props = { params: Promise<{ id: string }> };

export async function generateStaticParams() {
  const products = await getProducts();
  return products.map((product) => ({ id: product.id }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  const product = await getProduct(id);
  if (!product) {
    return { title: "Товар не найден" };
  }
  return { title: product.name, description: product.description };
}

export default async function ProductPage({ params }: Props) {
  const { id } = await params;
  const product = await getProduct(id);
  if (!product) {
    notFound();
  }

  return (
    <article>
      <h1>{product.name}</h1>
      <p>{product.description}</p>
      <p>Цена: {product.price} ₽</p>
    </article>
  );
}
