export function parseScore(text: string): number {
  const trimmed: string = text.trim();
  if (!/^\d+$/.test(trimmed)) {
    throw new Error("оценка должна быть целым числом от 0 до 100");
  }
  const score: number = Number(trimmed);
  if (score > 100) {
    throw new Error("оценка должна быть целым числом от 0 до 100");
  }
  return score;
}

export function safeScore(text: string): number | null {
  try {
    return parseScore(text);
  } catch (error) {
    return null;
  }
}

export function summarize(text: string): { count: number; average: number; skipped: number } {
  let count: number = 0;
  let sum: number = 0;
  let skipped: number = 0;
  for (const line of text.split(/\r?\n/)) {
    if (line.trim() === "") {
      continue;
    }
    const score: number | null = safeScore(line);
    if (score === null) {
      skipped++;
    } else {
      count++;
      sum += score;
    }
  }
  const average: number = count === 0 ? 0 : Math.round((sum / count) * 100) / 100;
  return { count, average, skipped };
}

export function describeFailure(error: unknown): string {
  if (error instanceof Error) {
    return "ошибка: " + error.message;
  }
  return "неизвестная ошибка";
}
