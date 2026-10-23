export class ParseError extends Error {
  line: number;

  constructor(message: string, line: number) {
    super(message);
    this.name = "ParseError";
    this.line = line;
  }
}

export interface OrderItem {
  name: string;
  quantity: number;
}

export interface LineProblem {
  line: number;
  message: string;
}

export function parseLine(line: string, lineNo: number): OrderItem {
  const parts: string[] = line.split(",");
  if (parts.length !== 2) {
    throw new ParseError("ожидалось два поля", lineNo);
  }
  const name: string = parts[0].trim();
  const quantityText: string = parts[1].trim();
  if (name === "") {
    throw new ParseError("пустое название", lineNo);
  }
  if (!/^\d+$/.test(quantityText)) {
    throw new ParseError("количество должно быть целым числом", lineNo);
  }
  const quantity: number = Number(quantityText);
  if (quantity < 1 || quantity > 100) {
    throw new ParseError("количество вне диапазона 1-100", lineNo);
  }
  return { name, quantity };
}

export function parseOrder(text: string): { items: OrderItem[]; problems: LineProblem[] } {
  const items: OrderItem[] = [];
  const problems: LineProblem[] = [];
  const lines: string[] = text.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].trim() === "") {
      continue;
    }
    try {
      items.push(parseLine(lines[i], i + 1));
    } catch (error) {
      if (error instanceof ParseError) {
        problems.push({ line: error.line, message: error.message });
      } else {
        throw error;
      }
    }
  }
  return { items, problems };
}

export function parseStringList(text: string): string[] | null {
  let data: unknown;
  try {
    data = JSON.parse(text);
  } catch (error) {
    if (error instanceof SyntaxError) {
      return null;
    }
    throw error;
  }
  if (!Array.isArray(data)) {
    return null;
  }
  const result: string[] = [];
  for (const element of data) {
    if (typeof element !== "string") {
      return null;
    }
    result.push(element);
  }
  return result;
}

export function errorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === "string") {
    return error;
  }
  return "неизвестная ошибка";
}
