const lines = require("node:fs").readFileSync(0, "utf8").split("\n");
const n = Number(lines[0]);

function deepEqual(a, b) {
  // TODO: сравните содержимое, а не ссылки
  return false;
}

for (let i = 1; i <= n; i++) {
  const [left, right] = lines[i].split(" | ");
  const a = JSON.parse(left);
  const b = JSON.parse(right);
  console.log(`strict=${a === b} loose=${a == b} deep=${deepEqual(a, b)}`);
}
