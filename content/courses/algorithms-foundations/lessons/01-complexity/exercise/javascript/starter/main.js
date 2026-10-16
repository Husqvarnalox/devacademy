const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Максимальная сумма непустого непрерывного отрезка массива a.
// Суммы не превосходят 2e14 < 2^53, поэтому обычного Number достаточно.
function bestSegment(a) {
  // TODO: верните максимальную сумму непустого отрезка за один проход (O(n))
  return 0;
}

const n = data[0];
console.log(String(bestSegment(data.slice(1, 1 + n))));
