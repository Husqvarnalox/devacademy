import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";

// TODO: сделайте компонент клиентским и пометьте текущую ссылку атрибутом aria-current="page".
export default function NavLink({ href, children }: { href: string; children: ReactNode }) {
  return <Link href={href}>{children}</Link>;
}
