const lines = require("node:fs").readFileSync(0, "utf8").split("\n");
const n = Number(lines[0]);

function deepEqual(a, b) {
  if (a === b) return true;
  if (typeof a !== "object" || typeof b !== "object" || a === null || b === null) return false;
  if (Array.isArray(a) !== Array.isArray(b)) return false;
  const ka = Object.keys(a);
  const kb = Object.keys(b);
  if (ka.length !== kb.length) return false;
  return ka.every((k) => Object.hasOwn(b, k) && deepEqual(a[k], b[k]));
}

for (let i = 1; i <= n; i++) {
  const [left, right] = lines[i].split(" | ");
  const a = JSON.parse(left);
  const b = JSON.parse(right);
  console.log(`strict=${a === b} loose=${a == b} deep=${deepEqual(a, b)}`);
}
