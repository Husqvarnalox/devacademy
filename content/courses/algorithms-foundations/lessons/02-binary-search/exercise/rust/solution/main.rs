use std::io::{self, Read};

// Сколько кусков длины length получится из всех досок (сумма выходит за i32, поэтому i64).
fn pieces(a: &[i64], length: i64) -> i64 {
    a.iter().map(|&x| x / length).sum()
}

// Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
fn longest_piece(a: &[i64], k: i64) -> i64 {
    let mut lo: i64 = 0; // инвариант: lo подходит (0 - условно), hi не подходит
    let mut hi: i64 = *a.iter().max().unwrap() + 1;
    while hi - lo > 1 {
        let mid = lo + (hi - lo) / 2;
        if pieces(a, mid) >= k {
            lo = mid;
        } else {
            hi = mid;
        }
    }
    lo
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let k: i64 = it.next().unwrap().parse().unwrap();
    let a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    println!("{}", longest_piece(&a, k));
}
