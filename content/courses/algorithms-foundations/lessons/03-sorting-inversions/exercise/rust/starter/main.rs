use std::io::{self, Read};

// Сортирует a на месте (tmp - буфер той же длины) и возвращает число пар i < j с a[i] > a[j].
fn sort_count(a: &mut [i64], tmp: &mut [i64]) -> i64 {
    // TODO: сортировка слиянием, которая заодно считает инверсии (O(n log n))
    let _ = (a, tmp);
    0
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let mut a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    let mut tmp = vec![0i64; n];
    println!("{}", sort_count(&mut a, &mut tmp));
}
