import type { ReactNode } from "react";

// TODO: сделайте компонент клиентским: заголовок-кнопка с aria-expanded показывает и скрывает children.
export default function Collapsible({
  title,
  children,
  defaultOpen = false,
}: {
  title: string;
  children: ReactNode;
  defaultOpen?: boolean;
}) {
  return (
    <section>
      <button type="button">{title}</button>
    </section>
  );
}
