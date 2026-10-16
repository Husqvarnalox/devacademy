use std::collections::HashMap;
use std::io::{self, Read};

// Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными). Суммы и ответ - i64.
fn count_segments(a: &[i64], k: i64) -> i64 {
    // TODO: префиксные суммы + HashMap "сумма -> сколько раз встречалась", O(n)
    let _ = (a, k);
    0
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let k: i64 = it.next().unwrap().parse().unwrap();
    let a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    println!("{}", count_segments(&a, k));
}
