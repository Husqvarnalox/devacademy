export interface User {
  name: string;
  age: number;
}

export function isStringArray(value: unknown): value is string[] {
  if (!Array.isArray(value)) {
    return false;
  }
  return value.every((item) => typeof item === "string");
}

export function parseUser(text: string): User | undefined {
  let data: unknown;
  try {
    data = JSON.parse(text);
  } catch {
    return undefined;
  }
  if (typeof data !== "object" || data === null || Array.isArray(data)) {
    return undefined;
  }
  const record = data as Record<string, unknown>;
  const name = record.name;
  const age = record.age;
  if (typeof name !== "string" || name.trim() === "") {
    return undefined;
  }
  if (typeof age !== "number" || !Number.isInteger(age) || age < 0) {
    return undefined;
  }
  return { name: name.trim(), age };
}

export function describeUnknown(value: unknown): string {
  if (value === null) {
    return "null";
  }
  if (value === undefined) {
    return "undefined";
  }
  if (typeof value === "string") {
    return "строка: " + value;
  }
  if (typeof value === "number") {
    return "число: " + value;
  }
  if (typeof value === "boolean") {
    return "логическое: " + value;
  }
  if (Array.isArray(value)) {
    return "массив из " + value.length;
  }
  return "объект";
}

export function sumNumbers(values: readonly unknown[]): number {
  let total = 0;
  for (const value of values) {
    if (typeof value === "number" && Number.isFinite(value)) {
      total += value;
    }
  }
  return total;
}
