"use strict";
// Ничего не слушает и не открывает сокетов: разбирает один HTTP-запрос из stdin.
const fs = require("node:fs");
const http = require("node:http");

function response(status, body, { type = "text/plain; charset=utf-8", allow, head = false } = {}) {
  const bytes = Buffer.byteLength(body);
  const lines = [`HTTP/1.1 ${status} ${http.STATUS_CODES[status]}`, `Content-Type: ${type}`, `Content-Length: ${bytes}`];
  if (allow) lines.push(`Allow: ${allow}`);
  lines.push("Connection: close", "");
  return lines.join("\n") + "\n" + (head ? "" : body);
}

function fail(status) {
  return response(status, `${status} ${http.STATUS_CODES[status]}`);
}

function splitMessage(raw) {
  const text = raw.toString("latin1");
  const m = /\r?\n\r?\n/.exec(text);
  if (!m) return { head: raw.toString("utf8"), body: Buffer.alloc(0) };
  return { head: raw.subarray(0, m.index).toString("utf8"), body: raw.subarray(m.index + m[0].length) };
}

const ROUTES = {
  "/": { GET: () => "Welcome" },
  "/hello": { GET: (req) => `Hello, ${req.url.searchParams.get("name") ?? "world"}!` },
  "/headers": {
    GET: (req) => {
      const sorted = Object.fromEntries(Object.entries(req.headers).sort(([a], [b]) => (a < b ? -1 : 1)));
      return { type: "application/json", body: JSON.stringify(sorted) };
    },
  },
  "/echo": {
    POST: (req) => ({ type: req.headers["content-type"] ?? "text/plain; charset=utf-8", body: req.body.toString("utf8") }),
  },
  "/sum": {
    POST: (req) => {
      let data;
      try {
        data = JSON.parse(req.body.toString("utf8"));
      } catch {
        return 400;
      }
      const nums = data && data.numbers;
      if (!Array.isArray(nums) || !nums.every((n) => typeof n === "number" && Number.isFinite(n))) return 400;
      return { type: "application/json", body: JSON.stringify({ sum: nums.reduce((a, b) => a + b, 0) }) };
    },
  },
};

function handle(raw) {
  const { head, body } = splitMessage(raw);
  const lines = head.split(/\r?\n/);
  const parts = lines[0].split(" ");
  if (parts.length !== 3 || parts.some((p) => p === "")) return fail(400);
  const [method, target, version] = parts;
  const vm = /^HTTP\/(\d)\.(\d)$/.exec(version);
  if (!vm) return fail(400);
  if (version !== "HTTP/1.1" && version !== "HTTP/1.0") return fail(505);

  const headers = {};
  for (const line of lines.slice(1)) {
    if (line === "") continue;
    const i = line.indexOf(":");
    if (i <= 0 || /\s/.test(line.slice(0, i))) return fail(400);
    const name = line.slice(0, i).toLowerCase();
    const value = line.slice(i + 1).trim();
    headers[name] = name in headers ? `${headers[name]}, ${value}` : value;
  }
  if (version === "HTTP/1.1" && !("host" in headers)) return fail(400);
  if (!http.METHODS.includes(method)) return fail(501);
  if (!target.startsWith("/")) return fail(400);

  const url = new URL(target, "http://localhost");
  const route = ROUTES[url.pathname];
  if (!route) return fail(404);
  const isHead = method === "HEAD";
  const handler = route[isHead ? "GET" : method];
  if (!handler) {
    const allowed = Object.keys(route);
    if (route.GET) allowed.push("HEAD");
    return response(405, `405 ${http.STATUS_CODES[405]}`, { allow: allowed.join(", ") });
  }

  let payload = body;
  if (method === "POST") {
    if (!("content-length" in headers)) return fail(411);
    if (!/^\d+$/.test(headers["content-length"])) return fail(400);
    const len = Number(headers["content-length"]);
    if (body.length < len) return fail(400);
    payload = body.subarray(0, len);
  }

  const result = handler({ url, headers, body: payload });
  if (typeof result === "number") return fail(result);
  if (typeof result === "string") return response(200, result, { head: isHead });
  return response(200, result.body, { type: result.type, head: isHead });
}

console.log(handle(fs.readFileSync(0)));
