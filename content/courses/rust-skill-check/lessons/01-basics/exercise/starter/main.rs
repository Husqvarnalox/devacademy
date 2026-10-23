fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut days = 0;
    // TODO: заведите сумму (тип i64), наибольшее значение и счётчик дней с целью 10000 шагов и более
    for token in text.split_whitespace() {
        let steps: i64 = token.parse().unwrap();
        days += 1;
        // TODO: накопите сумму, обновите наибольшее значение и посчитайте дни с целью
        let _ = steps;
    }
    // TODO: среднее = сумма / число дней как f64; для нуля дней - 0.00
    let average = -1.0;
    println!("days={days}");
    println!("total={}", -1);
    println!("best={}", -1);
    println!("avg={:.2}", average);
    println!("goal={}", -1);
}
