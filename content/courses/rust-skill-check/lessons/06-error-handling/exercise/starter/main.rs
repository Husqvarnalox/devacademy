use std::fmt;
use std::num::ParseIntError;

#[derive(Debug)]
enum LineError {
    MissingField(&'static str),
    BadNumber(ParseIntError),
    TooMany(u32),
}

// Тексты: "missing поле", "bad number: <сообщение ошибки>", "too many: n".
impl fmt::Display for LineError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        // TODO: match self { ... } и write!(f, ...)
        write!(f, "error")
    }
}

// Нужна, чтобы оператор ? мог превращать ParseIntError в LineError.
impl From<ParseIntError> for LineError {
    fn from(e: ParseIntError) -> Self {
        // TODO: верните вариант BadNumber
        LineError::MissingField("name")
    }
}

// "товар количество": разберите слова, количество - u32 и не больше 100.
fn parse_line(line: &str) -> Result<(String, u32), LineError> {
    // TODO: split_whitespace, ok_or(...)? и parse()?
    Err(LineError::MissingField("name"))
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut ok = 0;
    let mut failed = 0;
    for line in input.lines() {
        match parse_line(line) {
            Ok((name, qty)) => {
                ok += 1;
                println!("ok {} x{}", name, qty);
            }
            Err(e) => {
                failed += 1;
                println!("error: {}", e);
            }
        }
    }
    println!("ok={} error={}", ok, failed);
}
