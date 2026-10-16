// Режим разработки без Nest CLI: tsc --watch пересобирает dist/, node --watch перезапускает сервер.
import { spawn, spawnSync } from "node:child_process";
import { resolve } from "node:path";

const tsc = resolve("node_modules", "typescript", "bin", "tsc");

const first = spawnSync(process.execPath, [tsc, "-p", "tsconfig.json"], { stdio: "inherit" });
if (first.status !== 0) process.exit(first.status ?? 1);

const children = [
  spawn(process.execPath, [tsc, "-p", "tsconfig.json", "--watch", "--preserveWatchOutput"], { stdio: "inherit" }),
  spawn(process.execPath, ["--watch", "dist/main.js"], { stdio: "inherit" }),
];

const stop = () => children.forEach((c) => c.kill());
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
children.forEach((c) => c.on("exit", () => { stop(); process.exit(0); }));
