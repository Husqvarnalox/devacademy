import { readFileSync } from "node:fs";

interface Stats {
  sum: number;
  min: number;
  max: number;
  avg: number;
}

function summarize(nums: number[]): Stats {
  let sum = 0;
  let min = nums[0] as number;
  let max = nums[0] as number;
  for (const x of nums) {
    sum += x;
    if (x < min) min = x;
    if (x > max) max = x;
  }
  return { sum, min, max, avg: sum / nums.length };
}

const tokens: string[] = readFileSync(0, "utf8").trim().split(/\s+/);
const n: number = Number(tokens[0]);
const nums: number[] = tokens.slice(1, 1 + n).map(Number);
const s = summarize(nums);
console.log(`sum=${s.sum}`);
console.log(`min=${s.min}`);
console.log(`max=${s.max}`);
console.log(`avg=${s.avg.toFixed(2)}`);
