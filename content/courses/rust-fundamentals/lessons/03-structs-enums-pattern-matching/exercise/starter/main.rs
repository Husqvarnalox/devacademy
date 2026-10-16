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

// TODO: верните текст ошибки (match по всем вариантам Fault):
//   Unknown(name)    -> unknown command '<name>'
//   BadNumber(tok)   -> bad number '<tok>'
//   MissingNumber    -> missing number
//   Underflow        -> stack underflow
//   Overflow         -> overflow
fn message(_f: &Fault) -> String {
    todo!("сообщение об ошибке")
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

// TODO: выполните команду над стеком; верните Some(ошибка) при сбое и None при успехе.
//   Add и Mul берут два верхних значения (checked_add / checked_mul, при переполнении - Overflow),
//   Pop, Dup, Neg, Swap требуют нужного числа элементов, иначе Underflow.
fn apply(_stack: &mut Vec<i64>, _cmd: Cmd) -> Option<Fault> {
    todo!("выполнение команды")
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
