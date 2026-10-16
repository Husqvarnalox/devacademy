export function Badge({ text, tone = "neutral" }) {
  return <span data-tone={tone}>{text}</span>;
}
