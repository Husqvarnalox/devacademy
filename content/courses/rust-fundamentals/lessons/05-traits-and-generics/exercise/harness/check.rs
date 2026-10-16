mod shapes;

use shapes::*;
use std::fmt::Write as _;
use std::panic::{self, AssertUnwindSafe};

struct Plain;
impl Shape for Plain {
    fn area(&self) -> f64 {
        2.0
    }
    fn perimeter(&self) -> f64 {
        3.0
    }
}

fn close(a: f64, b: f64) -> bool {
    (a - b).abs() < 1e-9
}

fn ensure(cond: bool, msg: &str) -> Result<(), String> {
    if cond { Ok(()) } else { Err(msg.to_string()) }
}

fn boxed() -> Vec<Box<dyn Shape>> {
    vec![
        Box::new(Rect { w: 2.0, h: 3.0 }),
        Box::new(Circle { r: 1.0 }),
        Box::new(Rect { w: 1.0, h: 1.0 }),
    ]
}

fn run(nonce: &str, id: &str, f: impl FnOnce() -> Result<(), String>) {
    let outcome = panic::catch_unwind(AssertUnwindSafe(f));
    let (passed, message) = match outcome {
        Ok(Ok(())) => (true, String::new()),
        Ok(Err(m)) => (false, m),
        Err(_) => (false, "паника (todo!() или неверный индекс)".to_string()),
    };
    let mut safe = String::new();
    for c in message.chars() {
        match c {
            '"' | '\\' => safe.push(' '),
            '\n' | '\r' => safe.push(' '),
            _ => safe.push(c),
        }
    }
    let mut line = String::new();
    write!(line, "##DEVACADEMY## {nonce} {{\"id\":\"{id}\",\"passed\":{passed},\"message\":\"{safe}\"}}").unwrap();
    println!("{line}");
}

fn main() {
    panic::set_hook(Box::new(|_| {}));
    let nonce = std::env::var("DEVACADEMY_NONCE").unwrap_or_default();
    let n = nonce.as_str();

    run(n, "rect", || {
        let r = Rect { w: 2.0, h: 3.5 };
        ensure(close(r.area(), 7.0), "area прямоугольника 2 x 3.5 должна быть 7")?;
        ensure(close(r.perimeter(), 11.0), "perimeter прямоугольника 2 x 3.5 должен быть 11")?;
        ensure(r.name() == "rect", "name() прямоугольника должен быть rect")?;
        ensure(r.to_string() == "Rect(2x3.5)", "Display прямоугольника: Rect(2x3.5)")
    });
    run(n, "circle", || {
        let c = Circle { r: 2.0 };
        ensure(close(c.area(), std::f64::consts::PI * 4.0), "area круга радиуса 2 - PI * 4")?;
        ensure(close(c.perimeter(), 4.0 * std::f64::consts::PI), "perimeter круга радиуса 2 - 4 * PI")?;
        ensure(c.name() == "circle", "name() круга должен быть circle")
    });
    run(n, "total-area", || {
        let total = total_area(&boxed());
        ensure(close(total, 6.0 + std::f64::consts::PI + 1.0), "total_area суммирует площади через dyn Shape")?;
        ensure(close(total_area(&[]), 0.0), "для пустого среза сумма равна 0")
    });
    run(n, "max-of", || {
        ensure(max_of(&[3, 9, 2]) == Some(9), "max_of(&[3, 9, 2]) == Some(9)")?;
        ensure(max_of(&[1.5, -2.0]) == Some(1.5), "max_of работает и для f64 (PartialOrd)")?;
        ensure(max_of(&['a', 'z', 'q']) == Some('z'), "max_of работает для char")?;
        ensure(max_of::<i32>(&[]).is_none(), "для пустого среза max_of возвращает None")
    });
    run(n, "largest", || {
        let v = boxed();
        let best = largest(&v).ok_or("largest вернул None для непустого среза")?;
        ensure(best.name() == "rect" && close(best.area(), 6.0), "самая большая фигура - прямоугольник площадью 6")?;
        let tie: Vec<Box<dyn Shape>> = vec![Box::new(Rect { w: 2.0, h: 2.0 }), Box::new(Rect { w: 1.0, h: 4.0 }), Box::new(Plain)];
        let first = largest(&tie).ok_or("None для непустого среза")?;
        ensure(close(first.perimeter(), 8.0), "при равенстве площадей берётся первая фигура")?;
        let empty: Vec<Box<dyn Shape>> = Vec::new();
        ensure(largest(&empty).is_none(), "для пустого среза largest возвращает None")
    });
    run(n, "default-name", || {
        ensure(Plain.name() == "shape", "метод name по умолчанию должен возвращать shape")
    });
    run(n, "money-ops", || {
        ensure(Money(150) + Money(275) == Money(425), "Money + Money")?;
        ensure(-Money(40) == Money(-40), "-Money")?;
        ensure(Money(199) * 3 == Money(597), "Money * i64")?;
        let total: Money = vec![Money(100), Money(250), Money(-50)].into_iter().sum();
        ensure(total == Money(300), "Sum для Money")?;
        let empty: Money = Vec::<Money>::new().into_iter().sum();
        ensure(empty == Money(0), "сумма пустого итератора - Money(0)")
    });
    run(n, "money-display", || {
        ensure(Money(1999).to_string() == "19.99", "Money(1999) -> 19.99")?;
        ensure(Money(5).to_string() == "0.05", "Money(5) -> 0.05")?;
        ensure(Money(-5).to_string() == "-0.05", "Money(-5) -> -0.05")?;
        ensure(Money(-12345).to_string() == "-123.45", "Money(-12345) -> -123.45")?;
        ensure(Money(0).to_string() == "0.00", "Money(0) -> 0.00")
    });
}
