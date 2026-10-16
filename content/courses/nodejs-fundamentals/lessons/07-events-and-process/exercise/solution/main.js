"use strict";
const { EventEmitter } = require("node:events");
const readline = require("node:readline");

let status = 0;

// Синхронный обработчик: на событии 'exit' асинхронная работа уже невозможна.
process.on("exit", () => {
  console.log(`bye status=${status}`);
});

function interpret(line, emitter, registry) {
  const [cmd, event, ...rest] = line.trim().split(/\s+/);
  const key = (name) => `${event}\u0000${name}`;
  const make = (name) => (...args) => console.log(`${name}(${args.map(String).join(", ")})`);

  switch (cmd) {
    case "on":
    case "once":
    case "prepend": {
      const name = rest[0];
      const fn = make(name);
      const list = registry.get(key(name)) ?? [];
      list.push(fn);
      registry.set(key(name), list);
      if (cmd === "on") emitter.on(event, fn);
      else if (cmd === "once") emitter.once(event, fn);
      else emitter.prependListener(event, fn);
      return "ok";
    }
    case "off": {
      const list = registry.get(key(rest[0])) ?? [];
      const fn = list.pop();
      const before = emitter.listenerCount(event);
      if (fn) emitter.removeListener(event, fn);
      return emitter.listenerCount(event) < before ? "ok" : "none";
    }
    case "emit": {
      try {
        const args = event === "error" ? [new Error(rest.join(" "))] : rest;
        const had = emitter.emit(event, ...args);
        return `emitted ${event}: ${had}`;
      } catch (err) {
        return `unhandled: ${err.message}`;
      }
    }
    case "count":
      return String(emitter.listenerCount(event));
    case "names": {
      const names = emitter.eventNames().map(String).sort();
      return names.length === 0 ? "(none)" : names.join(" ");
    }
    default:
      return "error BADCMD";
  }
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
