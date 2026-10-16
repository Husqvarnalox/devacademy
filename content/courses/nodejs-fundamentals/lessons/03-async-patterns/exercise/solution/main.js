const { text } = require("node:stream/consumers");

// Одна попытка: k-я попытка берёт k-й результат (последний повторяется).
async function attempt(job, k) {
  await null;
  const token = job.outcomes[Math.min(k, job.outcomes.length) - 1];
  if (token.startsWith("ok:")) return token.slice(3);
  throw new Error(token.slice(4));
}

async function retry(job, maxAttempts) {
  let lastError;
  for (let k = 1; k <= maxAttempts; k++) {
    try {
      return { value: await attempt(job, k), attempts: k };
    } catch (error) {
      lastError = error;
    }
  }
  throw new Error(`gave up after ${maxAttempts} attempts`, { cause: lastError });
}

async function main() {
  const lines = (await text(process.stdin)).split("\n").map((l) => l.trim()).filter((l) => l !== "");
  const maxAttempts = Number(lines[0]);
  const jobs = lines.slice(1).map((line) => {
    const space = line.indexOf(" ");
    return { name: line.slice(0, space), outcomes: line.slice(space + 1).trim().split(",") };
  });

  const results = await Promise.allSettled(jobs.map((job) => retry(job, maxAttempts)));

  let ok = 0;
  results.forEach((result, i) => {
    const name = jobs[i].name;
    if (result.status === "fulfilled") {
      ok++;
      console.log(`${name}: ok value=${result.value.value} attempts=${result.value.attempts}`);
    } else {
      console.log(`${name}: failed attempts=${maxAttempts} cause=${result.reason.cause.message}`);
    }
  });
  console.log(`summary: ${ok} ok, ${results.length - ok} failed`);
}

main();
