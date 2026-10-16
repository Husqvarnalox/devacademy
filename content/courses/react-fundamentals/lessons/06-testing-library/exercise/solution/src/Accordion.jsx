import { useState } from "react";

export function Accordion({ title, children, defaultOpen = false }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <section>
      <h3>
        <button type="button" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
          {title}
        </button>
      </h3>
      {open && <div>{children}</div>}
    </section>
  );
}
