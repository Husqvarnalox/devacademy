use std::io::{self, Read};

// Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
fn shortest_exit(grid: &[&[u8]], n: usize, m: usize) -> i32 {
    let (mut start, mut end) = (0, 0);
    for r in 0..n {
        for c in 0..m {
            match grid[r][c] {
                b'S' => start = r * m + c,
                b'E' => end = r * m + c,
                _ => {}
            }
        }
    }
    // состояние = (клетка, сколько стен уже пробито: 0 или 1), кодируем числом cell * 2 + used
    let mut dist = vec![-1i32; 2 * n * m];
    let mut queue: Vec<usize> = Vec::with_capacity(2 * n * m);
    dist[start * 2] = 0;
    queue.push(start * 2);
    let mut head = 0;
    while head < queue.len() {
        let state = queue[head];
        head += 1;
        let (cell, used) = (state >> 1, state & 1);
        let d = dist[state] + 1;
        let (r, c) = ((cell / m) as i64, (cell % m) as i64);
        for (dr, dc) in [(1i64, 0i64), (-1, 0), (0, 1), (0, -1)] {
            let (nr, nc) = (r + dr, c + dc);
            if nr < 0 || nr >= n as i64 || nc < 0 || nc >= m as i64 {
                continue;
            }
            let (nr, nc) = (nr as usize, nc as usize);
            let nused = used + usize::from(grid[nr][nc] == b'#');
            if nused > 1 {
                continue;
            }
            let nstate = (nr * m + nc) * 2 + nused;
            if dist[nstate] < 0 {
                dist[nstate] = d;
                queue.push(nstate);
            }
        }
    }
    let (a, b) = (dist[end * 2], dist[end * 2 + 1]);
    match (a >= 0, b >= 0) {
        (false, false) => -1,
        (true, false) => a,
        (false, true) => b,
        (true, true) => a.min(b),
    }
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let m: usize = it.next().unwrap().parse().unwrap();
    let rows: Vec<&[u8]> = it.take(n).map(|row| row.as_bytes()).collect();
    println!("{}", shortest_exit(&rows, n, m));
}
