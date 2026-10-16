const data = require('fs').readFileSync(0, 'utf8').split(/\s+/).filter(Boolean);

// Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
function shortestExit(grid, n, m) {
  const flat = grid.join('');
  const start = flat.indexOf('S');
  const end = flat.indexOf('E');
  const wall = new Uint8Array(n * m);
  for (let i = 0; i < n * m; i++) wall[i] = flat[i] === '#' ? 1 : 0;
  // состояние = (клетка, сколько стен уже пробито: 0 или 1), кодируем числом cell * 2 + used
  const dist = new Int32Array(2 * n * m).fill(-1);
  const queue = new Int32Array(2 * n * m);
  let head = 0, tail = 0;
  dist[start * 2] = 0;
  queue[tail++] = start * 2;
  const dr = [1, -1, 0, 0];
  const dc = [0, 0, 1, -1];
  while (head < tail) {
    const state = queue[head++];
    const cell = state >> 1, used = state & 1;
    const d = dist[state] + 1;
    const r = Math.floor(cell / m), c = cell % m;
    for (let k = 0; k < 4; k++) {
      const nr = r + dr[k], nc = c + dc[k];
      if (nr < 0 || nr >= n || nc < 0 || nc >= m) continue;
      const ncell = nr * m + nc;
      const nused = used + wall[ncell];
      if (nused > 1) continue;
      const nstate = ncell * 2 + nused;
      if (dist[nstate] < 0) {
        dist[nstate] = d;
        queue[tail++] = nstate;
      }
    }
  }
  const a = dist[end * 2], b = dist[end * 2 + 1];
  if (a < 0 && b < 0) return -1;
  if (a < 0) return b;
  if (b < 0) return a;
  return Math.min(a, b);
}

const n = Number(data[0]);
const m = Number(data[1]);
console.log(String(shortestExit(data.slice(2, 2 + n), n, m)));
