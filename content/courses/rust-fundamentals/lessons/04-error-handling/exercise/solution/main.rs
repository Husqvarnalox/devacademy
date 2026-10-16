use std::fmt;
use std::io::Read;
use std::num::ParseIntError;

#[derive(Debug)]
enum CalcError {
    Parse(ParseIntError),
    Format,
    UnknownOp(String),
    DivByZero,
    Overflow,
}

impl fmt::Display for CalcError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            CalcError::Parse(e) => write!(f, "invalid number: {e}"),
            CalcError::Format => write!(f, "expected 'a op b'"),
            CalcError::UnknownOp(op) => write!(f, "unknown operator '{op}'"),
            CalcError::DivByZero => write!(f, "division by zero"),
            CalcError::Overflow => write!(f, "overflow"),
        }
    }
}

impl std::error::Error for CalcError {}

impl From<ParseIntError> for CalcError {
    fn from(e: ParseIntError) -> Self {
        CalcError::Parse(e)
    }
}

fn eval(line: &str) -> Result<i64, CalcError> {
    let parts: Vec<&str> = line.split_whitespace().collect();
    let [a, op, b] = parts[..] else {
        return Err(CalcError::Format);
    };
    let a: i64 = a.parse()?;
    let b: i64 = b.parse()?;
    let result = match op {
        "+" => a.checked_add(b),
        "-" => a.checked_sub(b),
        "*" => a.checked_mul(b),
        "/" | "%" if b == 0 => return Err(CalcError::DivByZero),
        "/" => a.checked_div(b),
        "%" => a.checked_rem(b),
        other => return Err(CalcError::UnknownOp(other.to_string())),
    };
    result.ok_or(CalcError::Overflow)
}

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    for line in input.lines().filter(|l| !l.trim().is_empty()) {
        match eval(line) {
            Ok(v) => println!("ok {v}"),
            Err(e) => println!("err {e}"),
        }
    }
}
