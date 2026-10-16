import { readFileSync } from "node:fs";

interface Stats {
  sum: number;
  min: number;
  max: number;
  avg: number;
}

function summarize(nums: number[]): Stats {
  // TODO: вычислите сумму, минимум, максимум и среднее
  return { sum: 0, min: 0, max: 0, avg: 0 };
}

const tokens: string[] = readFileSync(0, "utf8").trim().split(/\s+/);
const n: number = Number(tokens[0]);
const nums: number[] = []; // TODO: следующие n токенов как числа
const s = summarize(nums);
console.log(`sum=${s.sum}`);
console.log(`min=${s.min}`);
console.log(`max=${s.max}`);
console.log(`avg=${s.avg.toFixed(2)}`);
