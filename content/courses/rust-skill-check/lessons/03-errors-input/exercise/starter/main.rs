// Разбирает ВСЮ строку как целое число больше нуля; иначе None.
fn parse_positive_int(text: &str) -> Option<i32> {
    // TODO: parse::<i32>() возвращает Result; проверьте n > 0 и верните Some(n)
    text.parse::<i32>().ok()
}

// Разбирает ВСЮ строку как конечное число больше нуля; иначе None.
fn parse_positive_f64(text: &str) -> Option<f64> {
    // TODO: parse::<f64>() принимает и "nan", и "inf"; отсеките их через is_finite()
    text.parse::<f64>().ok()
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut total = 0.0;
    let mut bad = 0;
    for line in input.lines() {
        let words: Vec<&str> = line.split_whitespace().collect();
        if words.len() != 3 {
            bad += 1;
            continue;
        }
        let qty = parse_positive_int(words[1]);
        let price = parse_positive_f64(words[2]);
        match (qty, price) {
            (Some(q), Some(p)) => {
                let sum = q as f64 * p;
                total += sum;
                println!("{}: {:.2}", words[0], sum);
            }
            _ => bad += 1,
        }
    }
    println!("total={:.2} bad={}", total, bad);
}
