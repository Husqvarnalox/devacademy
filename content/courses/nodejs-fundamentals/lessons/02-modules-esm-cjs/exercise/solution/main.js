import { text } from "node:stream/consumers";

const lines = (await text(process.stdin)).split("\n").map((l) => l.trim()).filter((l) => l !== "");
const pkg = JSON.parse(lines[0]);

function normalizeExports(value) {
  const isSubpathMap =
    value !== null && typeof value === "object" && !Array.isArray(value) &&
    Object.keys(value).length > 0 && Object.keys(value).every((k) => k.startsWith("."));
  return isSubpathMap ? value : { ".": value };
}

// Возвращает строку, null (запрещено) или undefined (условия не подошли).
function resolveTarget(target, mode, star) {
  if (typeof target === "string") return star === undefined ? target : target.replaceAll("*", star);
  if (target === null) return null;
  if (typeof target === "object") {
    for (const [condition, value] of Object.entries(target)) {
      if (condition === "default" || condition === "node" || condition === mode) {
        const result = resolveTarget(value, mode, star);
        if (result !== undefined) return result;
      }
    }
  }
  return undefined;
}

function resolveExports(exportsField, mode, subpath) {
  const map = normalizeExports(exportsField);
  let result;
  if (Object.hasOwn(map, subpath) && !subpath.includes("*")) {
    result = resolveTarget(map[subpath], mode, undefined);
  } else {
    let best = null;
    for (const key of Object.keys(map)) {
      const star = key.indexOf("*");
      if (star < 0 || star !== key.lastIndexOf("*")) continue;
      const prefix = key.slice(0, star);
      const suffix = key.slice(star + 1);
      if (subpath.length >= key.length && subpath.startsWith(prefix) && subpath.endsWith(suffix)) {
        if (best === null || prefix.length > best.prefix.length || (prefix.length === best.prefix.length && key.length > best.key.length)) {
          best = { key, prefix, suffix };
        }
      }
    }
    if (best !== null) {
      const matched = subpath.slice(best.prefix.length, subpath.length - best.suffix.length);
      result = resolveTarget(map[best.key], mode, matched);
    }
  }
  return typeof result === "string" ? result : `ERR_PACKAGE_PATH_NOT_EXPORTED: ${subpath}`;
}

function moduleFormat(file) {
  if (file.endsWith(".mjs")) return "esm";
  if (file.endsWith(".cjs")) return "commonjs";
  if (file.endsWith(".json")) return "json";
  if (file.endsWith(".js")) return pkg.type === "module" ? "esm" : "commonjs";
  return "unknown";
}

for (const line of lines.slice(1)) {
  const [command, a, b] = line.split(/\s+/);
  if (command === "resolve") console.log(resolveExports(pkg.exports, a, b));
  else if (command === "format") console.log(`${a}: ${moduleFormat(a)}`);
  else console.log(`unknown command: ${command}`);
}
