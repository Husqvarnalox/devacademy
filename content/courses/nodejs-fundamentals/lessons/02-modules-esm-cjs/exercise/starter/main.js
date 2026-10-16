import { text } from "node:stream/consumers";

const lines = (await text(process.stdin)).split("\n").map((l) => l.trim()).filter((l) => l !== "");
const pkg = JSON.parse(lines[0]);

// Приводит поле exports к виду { "подпуть": цель }.
function normalizeExports(value) {
  // TODO: строка, массив и объект с условиями означают { ".": value }
  return value;
}

// Возвращает строку, null (путь запрещён) или undefined (ни одно условие не подошло).
function resolveTarget(target, mode, star) {
  // TODO: строка (подставьте star вместо *), null, объект условий (node, default, mode - по порядку ключей)
  return undefined;
}

function resolveExports(exportsField, mode, subpath) {
  // TODO: точное совпадение подпути, затем лучший шаблон с *, иначе ошибка
  return `ERR_PACKAGE_PATH_NOT_EXPORTED: ${subpath}`;
}

function moduleFormat(file) {
  // TODO: .mjs, .cjs, .json, .js (по pkg.type), остальное unknown
  return "unknown";
}

for (const line of lines.slice(1)) {
  const [command, a, b] = line.split(/\s+/);
  if (command === "resolve") console.log(resolveExports(pkg.exports, a, b));
  else if (command === "format") console.log(`${a}: ${moduleFormat(a)}`);
  else console.log(`unknown command: ${command}`);
}
