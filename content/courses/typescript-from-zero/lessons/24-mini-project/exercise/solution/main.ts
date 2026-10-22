import { readFileSync } from "node:fs";

interface Entry {
  category: string;
  amount: number;
}

interface Total {
  category: string;
  sum: number;
}

// Разбор одной строки вида "категория;сумма". Для неверной строки выбрасывает ошибку.
function parseEntry(line: string): Entry {
  const parts: string[] = line.split(";");
  if (parts.length !== 2) {
    throw new Error("нужны ровно две части");
  }
  const category: string = parts[0].trim();
  if (category === "") {
    throw new Error("пустая категория");
  }
  const amount: number = Number(parts[1].trim());
  if (Number.isNaN(amount) || amount <= 0) {
    throw new Error("сумма должна быть положительным числом");
  }
  return { category: category, amount: amount };
}

// Добавляет сумму к итогу категории; если категории ещё нет, создаёт её.
function addToTotals(totals: Total[], category: string, amount: number): void {
  const found = totals.find((t) => t.category === category);
  if (found === undefined) {
    totals.push({ category: category, sum: amount });
  } else {
    found.sum = found.sum + amount;
  }
}

const text: string = readFileSync(0, "utf8");
const lines: string[] = text.trim().split("\n");

const totals: Total[] = [];
let all: number = 0;
let valid: number = 0;
let bad: number = 0;

for (const rawLine of lines) {
  const line: string = rawLine.trim();
  if (line === "") {
    continue;
  }
  try {
    const entry: Entry = parseEntry(line);
    addToTotals(totals, entry.category, entry.amount);
    all = all + entry.amount;
    valid = valid + 1;
  } catch (error) {
    bad = bad + 1;
  }
}

const sorted: Total[] = [...totals].sort((a, b) => b.sum - a.sum);
for (const t of sorted) {
  console.log(t.category + ": " + t.sum.toFixed(2));
}
console.log("Итого: " + all.toFixed(2));
console.log("Записей: " + valid);
console.log("Неверных строк: " + bad);
