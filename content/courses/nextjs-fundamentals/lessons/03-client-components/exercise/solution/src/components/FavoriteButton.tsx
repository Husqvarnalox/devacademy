"use client";

import { useState } from "react";

export default function FavoriteButton({ title, initial = false }: { title: string; initial?: boolean }) {
  const [pressed, setPressed] = useState(initial);
  const label = pressed ? "В избранном" : "В избранное";

  return (
    <button type="button" aria-pressed={pressed} aria-label={`${title}: ${label}`} onClick={() => setPressed(!pressed)}>
      {label}
    </button>
  );
}
