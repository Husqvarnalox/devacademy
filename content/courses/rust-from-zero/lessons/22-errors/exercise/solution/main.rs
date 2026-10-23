fn parse_positive(text: &str) -> Result<i32, String> {
    match text.trim().parse::<i32>() {
        Ok(n) => {
            if n > 0 {
                Ok(n)
            } else {
                Err(format!("не положительное: {n}"))
            }
        }
        Err(_) => Err(format!("не число: {}", text.trim())),
    }
}

fn line_total(line: &str) -> Result<i32, String> {
    match line.split_once(';') {
        Some((qty_text, price_text)) => {
            let qty = parse_positive(qty_text)?;
            let price = parse_positive(price_text)?;
            Ok(qty * price)
        }
        None => Err(format!("нет разделителя: {line}")),
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut line_no = 0;
    let mut grand = 0;
    let mut bad = 0;
    for line in input.lines() {
        line_no += 1;
        match line_total(line) {
            Ok(total) => {
                println!("Строка {line_no}: сумма {total}");
                grand += total;
            }
            Err(msg) => {
                println!("Строка {line_no}: ошибка: {msg}");
                bad += 1;
            }
        }
    }
    println!("Итого: {grand}, ошибок: {bad}");
}
