import Link from "next/link";

export default function ProductsLayout({ children }: { children: React.ReactNode }) {
  return (
    <section aria-label="Каталог">
      <nav aria-label="Сортировка">
        <Link href="/products">По названию</Link> <Link href="/products?sort=price">По цене</Link>
      </nav>
      {children}
    </section>
  );
}
