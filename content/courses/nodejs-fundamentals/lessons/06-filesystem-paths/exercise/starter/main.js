"use strict";
const fs = require("node:fs/promises");
const path = require("node:path");
const readline = require("node:readline");

function resolveInside(root, p) {
  const full = path.resolve(root, p);
  const rel = path.relative(root, full);
  if (rel === ".." || rel.startsWith(".." + path.sep) || path.isAbsolute(rel)) return null;
  return full;
}

// TODO: реализуйте команды. Для ошибок файловой системы бросайте исключение дальше:
// main() печатает `error <код>` по полю err.code.
async function execute(root, cmd, a, b) {
  return "error BADCMD";
}

async function main() {
  const root = await fs.mkdtemp(path.join(process.cwd(), "sandbox-"));
  const out = [];
  try {
    const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
    for await (const line of rl) {
      const m = /^(\S+)(?: (\S+))?(?: (.*))?$/.exec(line.trim() === "" ? "" : line);
      if (!m) continue;
      const [, cmd, a, b] = m;
      try {
        out.push(await execute(root, cmd, a, b));
      } catch (err) {
        out.push(`error ${err.code}`);
      }
    }
  } finally {
    await fs.rm(root, { recursive: true, force: true });
  }
  process.stdout.write(out.join("\n") + "\n");
}

main();
