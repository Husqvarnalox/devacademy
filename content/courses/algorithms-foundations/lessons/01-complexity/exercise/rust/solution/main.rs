use std::io::{self, Read};

// Максимальная сумма непустого непрерывного отрезка. Суммы выходят за i32, поэтому i64.
fn best_segment(a: &[i64]) -> i64 {
    let mut best = a[0];
    let mut cur = a[0];
    for &x in &a[1..] {
        cur = x.max(cur + x);
        best = best.max(cur);
    }
    best
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    println!("{}", best_segment(&a));
}
