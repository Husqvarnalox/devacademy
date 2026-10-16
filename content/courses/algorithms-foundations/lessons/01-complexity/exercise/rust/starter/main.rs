use std::io::{self, Read};

// Максимальная сумма непустого непрерывного отрезка. Суммы выходят за i32, поэтому i64.
fn best_segment(a: &[i64]) -> i64 {
    // TODO: верните максимальную сумму непустого отрезка за один проход (O(n))
    let _ = a;
    0
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    println!("{}", best_segment(&a));
}
