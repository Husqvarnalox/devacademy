const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Сортирует a[lo, hi) на месте с помощью буфера tmp и возвращает число инверсий внутри этого куска.
// Число пар до ~2e10 безопасно помещается в Number (точен до 2^53).
function sortCount(a, tmp, lo, hi) {
  // TODO: сортировка слиянием, которая заодно считает инверсии (O(n log n))
  return 0;
}

const n = data[0];
const a = Int32Array.from(data.slice(1, 1 + n));
console.log(String(sortCount(a, new Int32Array(n), 0, n)));
