use std::f64::consts::PI;
use std::fmt;
use std::iter::Sum;
use std::ops::{Add, Mul, Neg};

pub trait Shape {
    fn area(&self) -> f64;
    fn perimeter(&self) -> f64;
    fn name(&self) -> String {
        "shape".to_string()
    }
}

pub struct Rect {
    pub w: f64,
    pub h: f64,
}

pub struct Circle {
    pub r: f64,
}

impl Shape for Rect {
    fn area(&self) -> f64 {
        self.w * self.h
    }
    fn perimeter(&self) -> f64 {
        2.0 * (self.w + self.h)
    }
    fn name(&self) -> String {
        "rect".to_string()
    }
}

impl Shape for Circle {
    fn area(&self) -> f64 {
        PI * self.r * self.r
    }
    fn perimeter(&self) -> f64 {
        2.0 * PI * self.r
    }
    fn name(&self) -> String {
        "circle".to_string()
    }
}

impl fmt::Display for Rect {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Rect({}x{})", self.w, self.h)
    }
}

pub fn total_area(shapes: &[Box<dyn Shape>]) -> f64 {
    shapes.iter().map(|s| s.area()).sum()
}

pub fn largest<'a>(shapes: &'a [Box<dyn Shape>]) -> Option<&'a dyn Shape> {
    let mut best: Option<&dyn Shape> = None;
    for s in shapes {
        match best {
            Some(b) if b.area() >= s.area() => {}
            _ => best = Some(s.as_ref()),
        }
    }
    best
}

pub fn max_of<T: PartialOrd + Copy>(xs: &[T]) -> Option<T> {
    let mut it = xs.iter().copied();
    let first = it.next()?;
    Some(it.fold(first, |m, x| if x > m { x } else { m }))
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Money(pub i64);

impl Add for Money {
    type Output = Money;
    fn add(self, o: Money) -> Money {
        Money(self.0 + o.0)
    }
}

impl Neg for Money {
    type Output = Money;
    fn neg(self) -> Money {
        Money(-self.0)
    }
}

impl Mul<i64> for Money {
    type Output = Money;
    fn mul(self, k: i64) -> Money {
        Money(self.0 * k)
    }
}

impl Sum for Money {
    fn sum<I: Iterator<Item = Money>>(iter: I) -> Money {
        iter.fold(Money(0), |a, b| a + b)
    }
}

impl fmt::Display for Money {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let sign = if self.0 < 0 { "-" } else { "" };
        let abs = self.0.abs();
        write!(f, "{}{}.{:02}", sign, abs / 100, abs % 100)
    }
}

fn main() {
    let shapes: Vec<Box<dyn Shape>> = vec![Box::new(Rect { w: 2.0, h: 3.0 }), Box::new(Circle { r: 1.0 })];
    println!("{:.2}", total_area(&shapes));
    println!("{}", Money(1999) * 2 + Money(-5));
}
