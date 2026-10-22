fn parse_positive(text: &str) -> Result<i32, String> {
    // TODO: разберите число и проверьте, что оно больше нуля
    Err(String::new())
}

fn line_total(line: &str) -> Result<i32, String> {
    // TODO: разделите строку по ';' и перемножьте обе части через parse_positive
    Err(String::new())
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
