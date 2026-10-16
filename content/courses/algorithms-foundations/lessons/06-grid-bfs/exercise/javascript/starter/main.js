const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean);

// Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
function shortestExit(grid, n, m) {
  // TODO: поиск в ширину по состояниям (клетка, использована ли уже пробивка стены), O(n * m)
  return -1;
}

const n = Number(data[0]);
const m = Number(data[1]);
console.log(String(shortestExit(data.slice(2, 2 + n), n, m)));
