"use strict";
const fs = require("node:fs");

// Весь ввод - это байты нескольких HTTP-запросов, идущих подряд.
const raw = fs.readFileSync(0);

// TODO: разберите запросы по очереди (см. условие) и заполните out.
const out = [];
let done = 0;

out.push(`requests=${done}`);
console.log(out.join("\n"));
