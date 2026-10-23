use std::fmt;
use std::num::ParseIntError;

#[derive(Debug)]
enum LineError {
    MissingField(&'static str),
    BadNumber(ParseIntError),
    TooMany(u32),
}

impl fmt::Display for LineError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match self {
            LineError::MissingField(field) => write!(f, "missing {}", field),
            LineError::BadNumber(e) => write!(f, "bad number: {}", e),
            LineError::TooMany(n) => write!(f, "too many: {}", n),
        }
    }
}

impl From<ParseIntError> for LineError {
    fn from(e: ParseIntError) -> Self {
        LineError::BadNumber(e)
    }
}

fn parse_line(line: &str) -> Result<(String, u32), LineError> {
    let mut words = line.split_whitespace();
    let name = words.next().ok_or(LineError::MissingField("name"))?;
    let qty_text = words.next().ok_or(LineError::MissingField("quantity"))?;
    let qty: u32 = qty_text.parse()?;
    if qty > 100 {
        return Err(LineError::TooMany(qty));
    }
    Ok((name.to_string(), qty))
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
