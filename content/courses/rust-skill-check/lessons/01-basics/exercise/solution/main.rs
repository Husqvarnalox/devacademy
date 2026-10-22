fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut days = 0;
    let mut total: i64 = 0;
    let mut best: i64 = 0;
    let mut goal = 0;
    for token in text.split_whitespace() {
        let steps: i64 = token.parse().unwrap();
        days += 1;
        total += steps;
        if steps > best {
            best = steps;
        }
        if steps >= 10000 {
            goal += 1;
        }
    }
    let average = if days == 0 { 0.0 } else { total as f64 / days as f64 };
    println!("days={days}");
    println!("total={total}");
    println!("best={best}");
    println!("avg={:.2}", average);
    println!("goal={goal}");
}
