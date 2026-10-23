// Разбор оценок из текста. Слово export делает функцию видимой для проверки.

// Оценка - целое число от 0 до 100 (пробелы по краям игнорируются).
// Иначе выбросьте new Error("оценка должна быть целым числом от 0 до 100").
// Пустая строка, "abc", "4.5", "-1" и "101" - неверные.
export function parseScore(text: string): number {
  // TODO: Number("") равно 0, поэтому проверяйте формат (например, /^\d+$/) до приведения
  return 0;
}

// То же, но вместо исключения возвращает null.
export function safeScore(text: string): number | null {
  // TODO: try / catch вокруг parseScore
  return null;
}

// Разбирает многострочный текст (\n или \r\n): пустые строки игнорируются совсем,
// неверные оценки считаются в skipped. average - среднее допустимых оценок, округлённое
// до двух знаков через Math.round(x * 100) / 100; если оценок нет, average равно 0.
export function summarize(text: string): { count: number; average: number; skipped: number } {
  // TODO
  return { count: 0, average: 0, skipped: 0 };
}

// Текст для пойманной ошибки: у Error - "ошибка: " и message, у всего остального - "неизвестная ошибка".
export function describeFailure(error: unknown): string {
  // TODO: error instanceof Error
  return "";
}
