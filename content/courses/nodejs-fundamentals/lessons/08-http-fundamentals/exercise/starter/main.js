"use strict";
// Ничего не слушает и не открывает сокетов: разбирает один HTTP-запрос из stdin.
const fs = require("node:fs");
const http = require("node:http");

// Соберите ответ: статусная строка, заголовки Content-Type, Content-Length (в байтах),
// при необходимости Allow, затем Connection: close, пустая строка и тело.
function handle(raw) {
  return "HTTP/1.1 500 " + http.STATUS_CODES[500];
}

console.log(handle(fs.readFileSync(0)));
