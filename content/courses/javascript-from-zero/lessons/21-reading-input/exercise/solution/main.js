// Читаем весь ввод одной строкой.
// require подключает встроенный модуль "node:fs" (работа с файлами),
// 0 - номер стандартного ввода, "utf8" - кодировка, чтобы получить текст.
const text = require("node:fs").readFileSync(0, "utf8");

const lines = text.trim().split("\n");
let count = 0;
let sum = 0;
let skipped = 0;

for (const rawLine of lines) {
  const line = rawLine.trim();
  if (line === "") {
    continue;
  }
  const value = Number(line);
  if (Number.isNaN(value)) {
    skipped++;
  } else {
    count++;
    sum = sum + value;
  }
}

let average = 0;
if (count > 0) {
  average = sum / count;
}

console.log("Чисел: " + count);
console.log("Сумма: " + sum.toFixed(2));
console.log("Среднее: " + average.toFixed(2));
console.log("Пропущено: " + skipped);
