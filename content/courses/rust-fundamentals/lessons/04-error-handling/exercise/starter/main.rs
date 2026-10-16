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

// TODO 1: реализуйте Display (match по всем вариантам):
//   Parse(e)      -> invalid number: <текст ошибки e>
//   Format        -> expected 'a op b'
//   UnknownOp(op) -> unknown operator '<op>'
//   DivByZero     -> division by zero
//   Overflow      -> overflow
impl fmt::Display for CalcError {
    fn fmt(&self, _f: &mut fmt::Formatter<'_>) -> fmt::Result {
        todo!("Display")
    }
}

impl std::error::Error for CalcError {}

// TODO 2: добавьте impl From<ParseIntError> for CalcError, чтобы работал оператор ?

// TODO 3: разберите строку "a op b" (три токена через пробелы):
//   не три токена -> Format; числа разбираются через ? (сначала a, потом b);
//   + - * используют checked_add / checked_sub / checked_mul, / и % - checked_div / checked_rem;
//   деление на ноль (b == 0 для / и %) -> DivByZero; другой оператор -> UnknownOp;
//   None из checked_* -> Overflow.
fn eval(_line: &str) -> Result<i64, CalcError> {
    todo!("вычисление")
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
