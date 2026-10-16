"use strict";
const { EventEmitter } = require("node:events");
const readline = require("node:readline");

let status = 0;

// Синхронный обработчик: на событии 'exit' асинхронная работа уже невозможна.
process.on("exit", () => {
  console.log(`bye status=${status}`);
});

// Команды: on/once/prepend <событие> <имя>, off <событие> <имя>,
// emit <событие> [аргументы...], count <событие>, names.
// Верните строку результата; если команда печатает сама (слушатели) - это нормально.
function interpret(line, emitter, registry) {
  return "error BADCMD";
}

async function main() {
  const emitter = new EventEmitter();
  const registry = new Map();
  const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
  for await (const line of rl) {
    if (line.trim() === "") continue;
    if (/^exit(\s|$)/.test(line.trim())) {
      status = Number(line.trim().split(/\s+/)[1] ?? 0);
      break;
    }
    console.log(interpret(line, emitter, registry));
  }
  rl.close();
}

main();
