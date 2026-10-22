const text = require("node:fs").readFileSync(0, "utf8");
const lines = text.trim().split("\n");

const totals = {};
let overall = 0;
let malformed = 0;

for (const rawLine of lines) {
  const line = rawLine.trim();
  if (line === "") {
    continue;
  }
  const parts = line.split(" ");
  if (parts.length !== 2) {
    malformed++;
    continue;
  }
  const category = parts[0];
  const amount = Number(parts[1]);
  if (Number.isNaN(amount)) {
    malformed++;
    continue;
  }
  if (!(category in totals)) {
    totals[category] = 0;
  }
  totals[category] = totals[category] + amount;
  overall = overall + amount;
}

const names = Object.keys(totals).toSorted();
let largest = "";
let largestTotal = 0;
for (const name of names) {
  console.log(name + ": " + totals[name].toFixed(2));
  if (largest === "" || totals[name] > largestTotal) {
    largest = name;
    largestTotal = totals[name];
  }
}
console.log("Итого: " + overall.toFixed(2));
if (largest === "") {
  largest = "нет";
}
console.log("Больше всего: " + largest);
console.log("Неверных строк: " + malformed);
