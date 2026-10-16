use std::io::Read;

enum Fault {
    Unknown(String),
    BadNumber(String),
    MissingNumber,
    Underflow,
    Overflow,
}

enum Cmd {
    Push(i64),
    Pop,
    Add,
    Mul,
    Dup,
    Swap,
    Neg,
    Invalid(Fault),
}

fn message(f: &Fault) -> String {
    match f {
        Fault::Unknown(name) => format!("unknown command '{name}'"),
        Fault::BadNumber(tok) => format!("bad number '{tok}'"),
        Fault::MissingNumber => "missing number".to_string(),
        Fault::Underflow => "stack underflow".to_string(),
        Fault::Overflow => "overflow".to_string(),
    }
}

fn parse(line: &str) -> Cmd {
    let mut parts = line.split_whitespace();
    let name = parts.next().unwrap_or("");
    match name {
        "push" => match parts.next() {
            None => Cmd::Invalid(Fault::MissingNumber),
            Some(tok) => match tok.parse::<i64>() {
                Ok(n) => Cmd::Push(n),
                Err(_) => Cmd::Invalid(Fault::BadNumber(tok.to_string())),
            },
        },
        "pop" => Cmd::Pop,
        "add" => Cmd::Add,
        "mul" => Cmd::Mul,
        "dup" => Cmd::Dup,
        "swap" => Cmd::Swap,
        "neg" => Cmd::Neg,
        other => Cmd::Invalid(Fault::Unknown(other.to_string())),
    }
}

fn apply(stack: &mut Vec<i64>, cmd: Cmd) -> Option<Fault> {
    match cmd {
        Cmd::Invalid(fault) => Some(fault),
        Cmd::Push(n) => {
            stack.push(n);
            None
        }
        Cmd::Pop => match stack.pop() {
            Some(_) => None,
            None => Some(Fault::Underflow),
        },
        Cmd::Dup => match stack.last().copied() {
            Some(top) => {
                stack.push(top);
                None
            }
            None => Some(Fault::Underflow),
        },
        Cmd::Neg => match stack.pop() {
            None => Some(Fault::Underflow),
            Some(top) => match top.checked_neg() {
                Some(v) => {
                    stack.push(v);
                    None
                }
                None => Some(Fault::Overflow),
            },
        },
        Cmd::Swap => {
            let len = stack.len();
            if len < 2 {
                return Some(Fault::Underflow);
            }
            stack.swap(len - 1, len - 2);
            None
        }
        Cmd::Add | Cmd::Mul => {
            let (Some(b), Some(a)) = (stack.pop(), stack.pop()) else {
                return Some(Fault::Underflow);
            };
            let result = if matches!(cmd, Cmd::Add) { a.checked_add(b) } else { a.checked_mul(b) };
            match result {
                Some(v) => {
                    stack.push(v);
                    None
                }
                None => Some(Fault::Overflow),
            }
        }
    }
}

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    let mut stack: Vec<i64> = Vec::new();
    for (i, line) in input.lines().enumerate() {
        if line.trim().is_empty() {
            continue;
        }
        if let Some(fault) = apply(&mut stack, parse(line)) {
            println!("error line {}: {}", i + 1, message(&fault));
            return;
        }
    }
    println!("stack={:?}", stack);
    println!("top={}", stack.last().map_or("none".to_string(), |v| v.to_string()));
}
