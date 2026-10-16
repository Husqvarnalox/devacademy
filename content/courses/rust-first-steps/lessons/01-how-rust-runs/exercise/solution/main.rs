use std::io::Read;

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    let mut it = input.split_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let nums: Vec<i64> = it.take(n).map(|t| t.parse().unwrap()).collect();
    let sum: i64 = nums.iter().sum();
    let min = *nums.iter().min().unwrap();
    let max = *nums.iter().max().unwrap();
    println!("sum={}", sum);
    println!("min={}", min);
    println!("max={}", max);
    println!("avg={:.2}", sum as f64 / n as f64);
}
