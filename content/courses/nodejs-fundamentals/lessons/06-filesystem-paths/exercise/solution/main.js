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

async function execute(root, cmd, a, b) {
  const full = a === undefined ? null : resolveInside(root, a);
  if (a !== undefined && full === null) return "error ESCAPE";
  switch (cmd) {
    case "write":
      await fs.writeFile(full, b ?? "");
      return "ok";
    case "append":
      await fs.appendFile(full, b ?? "");
      return "ok";
    case "read": {
      const text = await fs.readFile(full, "utf8");
      return text === "" ? "(empty)" : text;
    }
    case "mkdir":
      await fs.mkdir(full);
      return "ok";
    case "mkdirp":
      await fs.mkdir(full, { recursive: true });
      return "ok";
    case "ls": {
      const entries = await fs.readdir(full, { withFileTypes: true });
      const names = entries.map((e) => (e.isDirectory() ? e.name + "/" : e.name)).sort();
      return names.length === 0 ? "(empty)" : names.join(" ");
    }
    case "stat": {
      const st = await fs.stat(full);
      return st.isDirectory() ? "dir" : `file ${st.size}`;
    }
    case "rm":
      await fs.rm(full);
      return "ok";
    case "rmdir":
      await fs.rmdir(full);
      return "ok";
    case "cp":
    case "mv": {
      const to = resolveInside(root, b ?? "");
      if (to === null) return "error ESCAPE";
      if (cmd === "cp") await fs.copyFile(full, to, fs.constants.COPYFILE_EXCL);
      else await fs.rename(full, to);
      return "ok";
    }
    default:
      return "error BADCMD";
  }
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
