const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Сортирует a[lo, hi) на месте с помощью буфера tmp и возвращает число инверсий внутри этого куска.
// Число пар до ~2e10 безопасно помещается в Number (точен до 2^53).
function sortCount(a, tmp, lo, hi) {
  if (hi - lo <= 1) return 0;
  const mid = (lo + hi) >> 1;
  let inv = sortCount(a, tmp, lo, mid) + sortCount(a, tmp, mid, hi);
  let i = lo, j = mid, k = lo;
  while (i < mid && j < hi) {
    if (a[i] <= a[j]) {
      tmp[k++] = a[i++];
    } else {
      tmp[k++] = a[j++];
      inv += mid - i; // a[j] меньше всех оставшихся слева
    }
  }
  while (i < mid) tmp[k++] = a[i++];
  while (j < hi) tmp[k++] = a[j++];
  for (let t = lo; t < hi; t++) a[t] = tmp[t];
  return inv;
}

const n = data[0];
const a = Int32Array.from(data.slice(1, 1 + n));
console.log(String(sortCount(a, new Int32Array(n), 0, n)));
