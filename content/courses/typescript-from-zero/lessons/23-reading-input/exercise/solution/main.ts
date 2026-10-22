import { readFileSync } from "node:fs";

const text: string = readFileSync(0, "utf8");
const lines: string[] = text.trim().split("\n");

let count: number = 0;
let sum: number = 0;
let best: number = 0;
let skipped: number = 0;

for (const rawLine of lines) {
  const line: string = rawLine.trim();
  if (line === "") {
    continue;
  }
  const grade: number = Number(line);
  if (Number.isNaN(grade) || Math.floor(grade) !== grade || grade < 1 || grade > 5) {
    skipped = skipped + 1;
    continue;
  }
  count = count + 1;
  sum = sum + grade;
  if (grade > best) {
    best = grade;
  }
}

let average: number = 0;
if (count > 0) {
  average = sum / count;
}

console.log("Оценок: " + count);
console.log("Среднее: " + average.toFixed(2));
if (count > 0) {
  console.log("Лучшая: " + best);
} else {
  console.log("Лучшая: -");
}
console.log("Пропущено: " + skipped);
