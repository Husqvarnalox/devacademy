export function Card({ title, children, footer }) {
  return (
    <section>
      <h2>{title}</h2>
      <div data-slot="body">{children}</div>
      {footer ? <footer>{footer}</footer> : null}
    </section>
  );
}
