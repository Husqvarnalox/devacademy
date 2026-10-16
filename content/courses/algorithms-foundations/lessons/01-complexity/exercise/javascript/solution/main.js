const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean).map(Number);

// Максимальная сумма непустого непрерывного отрезка массива a.
// Суммы не превосходят 2e14 < 2^53, поэтому обычного Number достаточно.
function bestSegment(a) {
  let best = a[0];
  let cur = a[0];
  for (let i = 1; i < a.length; i++) {
    cur = Math.max(a[i], cur + a[i]); // продолжить отрезок или начать новый
    best = Math.max(best, cur);
  }
  return best;
}

const n = data[0];
console.log(String(bestSegment(data.slice(1, 1 + n))));
