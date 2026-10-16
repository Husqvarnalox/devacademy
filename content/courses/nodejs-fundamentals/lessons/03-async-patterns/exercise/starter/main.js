const { text } = require("node:stream/consumers");

// Одна попытка: k-я попытка берёт k-й результат (последний повторяется).
// Уже готова, менять не нужно.
async function attempt(job, k) {
  await null;
  const token = job.outcomes[Math.min(k, job.outcomes.length) - 1];
  if (token.startsWith("ok:")) return token.slice(3);
  throw new Error(token.slice(4));
}

// TODO: повторяйте attempt(job, k) для k = 1..maxAttempts до первого успеха.
// Успех: верните { value, attempts: k }.
// Если все попытки неудачны: бросьте new Error(`gave up after ${maxAttempts} attempts`, { cause: последняя ошибка }).
async function retry(job, maxAttempts) {
  return { value: await attempt(job, 1), attempts: 1 };
}

async function main() {
  const lines = (await text(process.stdin)).split("\n").map((l) => l.trim()).filter((l) => l !== "");
  const maxAttempts = Number(lines[0]);
  const jobs = lines.slice(1).map((line) => {
    const space = line.indexOf(" ");
    return { name: line.slice(0, space), outcomes: line.slice(space + 1).trim().split(",") };
  });

  // TODO: запустите retry для всех заданий одновременно и дождитесь всех (Promise.allSettled),
  // не останавливаясь на первой ошибке. Затем выведите по строке на задание, в порядке ввода,
  // и итоговую строку summary (формат в условии).
  const results = [];
  for (const job of jobs) results.push(await retry(job, maxAttempts));
  console.log(`summary: ${results.length} ok, 0 failed`);
}

main();
