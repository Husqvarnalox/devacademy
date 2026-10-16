use std::collections::HashMap;
use std::io::{self, Read};

// Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными). Суммы и ответ - i64.
fn count_segments(a: &[i64], k: i64) -> i64 {
    let mut seen: HashMap<i64, i64> = HashMap::with_capacity(a.len() + 1); // префиксная сумма -> сколько раз встречалась
    seen.insert(0, 1); // пустой префикс
    let mut cur: i64 = 0;
    let mut ans: i64 = 0;
    for &x in a {
        cur += x;
        ans += seen.get(&(cur - k)).copied().unwrap_or(0); // левые границы, для которых сумма отрезка ровно k
        *seen.entry(cur).or_insert(0) += 1;
    }
    ans
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
