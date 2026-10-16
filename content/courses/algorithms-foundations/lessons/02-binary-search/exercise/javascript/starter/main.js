const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Сколько кусков длины length получится из всех досок (сумма до 1e14 < 2^53, Number хватает).
function pieces(a, length) {
  let total = 0;
  for (const x of a) total += Math.floor(x / length);
  return total;
}

// Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
function longestPiece(a, k) {
  // TODO: бинарный поиск по ответу - длине куска (O(n log max(a)))
  return 0;
}

const n = data[0];
const k = data[1];
console.log(String(longestPiece(data.slice(2, 2 + n), k)));
