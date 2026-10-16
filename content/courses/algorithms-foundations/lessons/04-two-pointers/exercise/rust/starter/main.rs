use std::io::{self, Read};

// Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0). Суммы и ответ - i64.
fn count_segments(a: &[i64], limit: i64) -> i64 {
    // TODO: два указателя (скользящее окно), O(n)
    let _ = (a, limit);
    0
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let limit: i64 = it.next().unwrap().parse().unwrap();
    let a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    println!("{}", count_segments(&a, limit));
}
