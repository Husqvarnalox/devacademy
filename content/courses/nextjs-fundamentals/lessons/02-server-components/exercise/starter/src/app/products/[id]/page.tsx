import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getProduct, getProducts } from "../../../lib/products";

type Props = { params: Promise<{ id: string }> };

export async function generateStaticParams() {
  // TODO: верните по одному объекту { id } для каждого товара.
  return [];
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  // TODO: дождитесь params, найдите товар и верните { title, description } (для неизвестного id - { title: "Товар не найден" }).
  return {};
}

export default async function ProductPage({ params }: Props) {
  // TODO: дождитесь params, найдите товар через getProduct, для неизвестного id вызовите notFound(),
  // иначе выведите <article> с <h1>, описанием и ценой.
  return <p>TODO</p>;
}
