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
  // TODO: для каждого вида команды выберите свой способ отложить вывод label:
  // sync - сразу; tick - process.nextTick; micro - промис; timer - setTimeout(.., 0); immediate - setImmediate;
  // составные: tick>micro, micro>tick (см. условие). Пока всё печатается сразу, в порядке ввода.
  console.log(label);
}

text(process.stdin).then((input) => {
  const commands = parse(input);
  // Всё выполняется внутри колбэка ввода-вывода (фаза poll): так порядок setImmediate и setTimeout определён.
  fs.readFile(__filename, () => {
    for (const command of commands) schedule(command);
  });
});
