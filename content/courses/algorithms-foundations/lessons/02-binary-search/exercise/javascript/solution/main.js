const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Сколько кусков длины length получится из всех досок (сумма до 1e14 < 2^53, Number хватает).
function pieces(a, length) {
  let total = 0;
  for (const x of a) total += Math.floor(x / length);
  return total;
}

// Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
function longestPiece(a, k) {
  let lo = 0;                       // инвариант: lo подходит (0 - условно), hi не подходит
  let hi = 1;
  for (const x of a) if (x >= hi) hi = x + 1; // hi = max(a) + 1
  while (hi - lo > 1) {
    const mid = Math.floor((lo + hi) / 2);
    if (pieces(a, mid) >= k) lo = mid;
    else hi = mid;
  }
  return lo;
}

const n = data[0];
const k = data[1];
console.log(String(longestPiece(data.slice(2, 2 + n), k)));
