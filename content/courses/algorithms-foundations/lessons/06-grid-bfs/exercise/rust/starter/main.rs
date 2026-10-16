use std::io::{self, Read};

// Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
fn shortest_exit(grid: &[&[u8]], n: usize, m: usize) -> i32 {
    // TODO: поиск в ширину по состояниям (клетка, использована ли уже пробивка стены), O(n * m)
    let _ = (grid, n, m);
    -1
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
