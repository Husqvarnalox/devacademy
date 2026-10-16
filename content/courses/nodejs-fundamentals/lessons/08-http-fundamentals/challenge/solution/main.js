"use strict";
// Разбор потока из нескольких HTTP-запросов подряд (pipelining) без сокетов.
const fs = require("node:fs");

const raw = fs.readFileSync(0);
let pos = 0;

// Читает строку до \n (возможный \r перед ним отбрасывается). null, если строка не закончена.
function readLine() {
  const i = raw.indexOf(0x0a, pos);
  if (i === -1) return null;
  let end = i;
  if (end > pos && raw[end - 1] === 0x0d) end--;
  const line = raw.toString("latin1", pos, end);
  pos = i + 1;
  return line;
}

function readBytes(n) {
  if (pos + n > raw.length) return null;
  const chunk = raw.subarray(pos, pos + n);
  pos += n;
  return chunk;
}

function skipEol() {
  if (raw[pos] === 0x0d) pos++;
  if (raw[pos] === 0x0a) pos++;
}

const ROUTES = { "GET /": 200, "POST /echo": 200 };

const out = [];
let done = 0;
let n = 0;

outer: while (pos < raw.length) {
  n++;
  const requestLine = readLine();
  if (requestLine === null) { out.push(`#${n} incomplete`); break; }
  const parts = requestLine.split(" ");
  if (parts.length !== 3 || !/^HTTP\/1\.[01]$/.test(parts[2])) { out.push(`#${n} 400 bad request`); break; }
  const [method, target] = parts;

  const headers = {};
  for (;;) {
    const line = readLine();
    if (line === null) { out.push(`#${n} incomplete`); break outer; }
    if (line === "") break;
    const i = line.indexOf(":");
    if (i <= 0) { out.push(`#${n} 400 bad request`); break outer; }
    headers[line.slice(0, i).toLowerCase()] = line.slice(i + 1).trim();
  }

  let bodyBytes = 0;
  if ((headers["transfer-encoding"] ?? "").toLowerCase().includes("chunked")) {
    for (;;) {
      const sizeLine = readLine();
      if (sizeLine === null) { out.push(`#${n} incomplete`); break outer; }
      const size = parseInt(sizeLine.split(";")[0].trim(), 16);
      if (Number.isNaN(size) || size < 0) { out.push(`#${n} 400 bad request`); break outer; }
      if (size === 0) {
        for (;;) {
          const trailer = readLine();
          if (trailer === null) { out.push(`#${n} incomplete`); break outer; }
          if (trailer === "") break;
        }
        break;
      }
      if (readBytes(size) === null) { out.push(`#${n} incomplete`); break outer; }
      bodyBytes += size;
      skipEol();
    }
  } else if ("content-length" in headers) {
    if (!/^\d+$/.test(headers["content-length"])) { out.push(`#${n} 400 bad request`); break; }
    const len = Number(headers["content-length"]);
    if (readBytes(len) === null) { out.push(`#${n} incomplete`); break; }
    bodyBytes = len;
  }

  const status = ROUTES[`${method} ${target}`] ?? 404;
  out.push(`#${n} ${method} ${target} body=${bodyBytes} -> ${status}`);
  done++;
  if ((headers["connection"] ?? "").toLowerCase() === "close") {
    out.push("closed by client");
    break;
  }
}

out.push(`requests=${done}`);
console.log(out.join("\n"));
