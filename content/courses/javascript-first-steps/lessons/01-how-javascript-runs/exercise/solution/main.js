const text = require("node:fs").readFileSync(0, "utf8");
const tokens = text.trim().split(/\s+/);
const n = Number(tokens[0]);
const nums = tokens.slice(1, 1 + n).map(Number);

let sum = 0;
let min = nums[0];
let max = nums[0];
for (const x of nums) {
  sum += x;
  if (x < min) min = x;
  if (x > max) max = x;
}
console.log("sum=" + sum);
console.log("min=" + min);
console.log("max=" + max);
console.log("avg=" + (sum / nums.length).toFixed(2));
