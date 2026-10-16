import { text } from "node:stream/consumers";

const input = await text(process.stdin);

const totals = new Map();
for (const raw of input.split("\n")) {
  const line = raw.trim();
  if (line === "") continue;
  const [name, amount] = line.split(",").map((s) => s.trim());
  totals.set(name, (totals.get(name) ?? 0) + Number(amount));
}

for (const name of [...totals.keys()].sort()) {
  console.log(`${name}: ${totals.get(name)}`);
}
