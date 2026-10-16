use std::io::{self, Read};

// Сколько кусков длины length получится из всех досок (сумма выходит за i32, поэтому i64).
fn pieces(a: &[i64], length: i64) -> i64 {
    a.iter().map(|&x| x / length).sum()
}

// Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
fn longest_piece(a: &[i64], k: i64) -> i64 {
    // TODO: бинарный поиск по ответу - длине куска (O(n log max(a)))
    let _ = (a, k, pieces(a, 1));
    0
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
