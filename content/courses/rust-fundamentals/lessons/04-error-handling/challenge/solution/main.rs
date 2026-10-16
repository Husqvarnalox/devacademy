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

impl fmt::Display for RecordError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "line {}: ", self.line)?;
        match &self.kind {
            Kind::WrongFieldCount(n) => write!(f, "expected 3 fields, got {n}"),
            Kind::EmptyId => write!(f, "empty id"),
            Kind::BadQty(_) => write!(f, "bad quantity"),
            Kind::BadPrice(_) => write!(f, "bad price"),
        }
    }
}

impl Error for RecordError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match &self.kind {
            Kind::BadQty(e) | Kind::BadPrice(e) => Some(e),
            _ => None,
        }
    }
}

#[derive(Debug)]
struct Order {
    qty: u32,
    price: u64,
}

fn parse_record(line_no: usize, line: &str) -> Result<Order, RecordError> {
    let fail = |kind| RecordError { line: line_no, kind };
    let fields: Vec<&str> = line.split(',').map(str::trim).collect();
    let [id, qty, price] = fields[..] else {
        return Err(fail(Kind::WrongFieldCount(fields.len())));
    };
    if id.is_empty() {
        return Err(fail(Kind::EmptyId));
    }
    let qty = qty.parse().map_err(|e| fail(Kind::BadQty(e)))?;
    let price = price.parse().map_err(|e| fail(Kind::BadPrice(e)))?;
    Ok(Order { qty, price })
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
