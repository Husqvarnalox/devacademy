use std::io::Read;

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    let mut it = input.split_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let mut sum: i64 = 0;
    let mut min: i64 = 0;
    let mut max: i64 = 0;
    // TODO: пройдите по n числам (i64), обновите sum, min, max
    let _ = (n, &mut it);
    println!("sum={}", sum);
    println!("min={}", min);
    println!("max={}", max);
    println!("avg={:.2}", 0.0_f64);
}
