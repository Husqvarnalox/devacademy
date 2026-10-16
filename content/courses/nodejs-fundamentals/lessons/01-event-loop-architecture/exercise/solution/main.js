const fs = require("node:fs");
const { text } = require("node:stream/consumers");

function parse(input) {
  return input
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line !== "")
    .map((line) => {
      const i = line.indexOf(" ");
      return i < 0 ? [line, ""] : [line.slice(0, i), line.slice(i + 1).trim()];
    });
}

function schedule([kind, label]) {
  const say = (s) => () => console.log(s);
  switch (kind) {
    case "sync":
      console.log(label);
      break;
    case "tick":
      process.nextTick(say(label));
      break;
    case "micro":
      Promise.resolve().then(say(label));
      break;
    case "tick>micro":
      process.nextTick(() => {
        console.log(label);
        Promise.resolve().then(say(label + ">micro"));
      });
      break;
    case "micro>tick":
      Promise.resolve().then(() => {
        console.log(label);
        process.nextTick(say(label + ">tick"));
      });
      break;
    case "timer":
      setTimeout(say(label), 0);
      break;
    case "immediate":
      setImmediate(say(label));
      break;
    default:
      console.log("unknown: " + kind);
  }
}

text(process.stdin).then((input) => {
  const commands = parse(input);
  // Выполняем в колбэке ввода-вывода: он идёт в фазе poll, поэтому setImmediate гарантированно раньше таймера.
  fs.readFile(__filename, () => {
    for (const command of commands) schedule(command);
  });
});
