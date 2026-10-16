use std::error::Error;
use std::fmt;
use std::io::Read;
use std::num::ParseIntError;

#[derive(Debug)]
enum Kind {
    WrongFieldCount(usize),
    EmptyId,
    BadQty(ParseIntError),
    BadPrice(ParseIntError),
}

#[derive(Debug)]
struct RecordError {
    line: usize,
    kind: Kind,
}

// TODO 1: Display печатает "line <N>: <сообщение>", где сообщение:
//   WrongFieldCount(n) -> expected 3 fields, got <n>
//   EmptyId            -> empty id
//   BadQty(_)          -> bad quantity
//   BadPrice(_)        -> bad price
impl fmt::Display for RecordError {
    fn fmt(&self, _f: &mut fmt::Formatter<'_>) -> fmt::Result {
        todo!("Display")
    }
}

// TODO 2: source() возвращает вложенную ParseIntError для BadQty и BadPrice, иначе None
impl Error for RecordError {}

#[derive(Debug)]
struct Order {
    qty: u32,
    price: u64,
}

// TODO 3: разберите запись "id,qty,price" (поля обрезаются от пробелов):
//   не три поля -> WrongFieldCount(сколько получилось); пустой id -> EmptyId;
//   qty разбирается как u32 (ошибка -> BadQty), price как u64 (ошибка -> BadPrice).
fn parse_record(_line_no: usize, _line: &str) -> Result<Order, RecordError> {
    todo!("разбор записи")
}

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    let mut lines = input.lines().enumerate();
    let strict = lines.next().map(|(_, l)| l.trim() == "strict").unwrap_or(false);
    let results: Vec<Result<Order, RecordError>> = lines
        .filter(|(_, l)| !l.trim().is_empty())
        .map(|(i, l)| parse_record(i + 1, l))
        .collect();

    let (good, bad): (Vec<_>, Vec<_>) = results.into_iter().partition(|r| r.is_ok());
    let orders: Vec<Order> = good.into_iter().map(Result::unwrap).collect();
    let errors: Vec<RecordError> = bad.into_iter().map(|r| r.unwrap_err()).collect();

    if strict {
        if let Some(first) = errors.first() {
            println!("error: {first}");
            if let Some(cause) = first.source() {
                println!("  caused by: {cause}");
            }
            return;
        }
    } else {
        println!("skipped={}", errors.len());
    }
    let total: u64 = orders.iter().map(|o| u64::from(o.qty) * o.price).sum();
    println!("orders={}", orders.len());
    println!("total={total}");
}
