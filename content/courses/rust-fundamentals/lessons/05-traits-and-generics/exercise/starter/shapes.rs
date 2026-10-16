use std::f64::consts::PI;
use std::fmt;
use std::iter::Sum;
use std::ops::{Add, Mul, Neg};

pub trait Shape {
    fn area(&self) -> f64;
    fn perimeter(&self) -> f64;
    // Метод по умолчанию: оставьте как есть, Rect и Circle его переопределяют.
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

// TODO 1: Rect: area = w * h, perimeter = 2 * (w + h), name = "rect".
impl Shape for Rect {
    fn area(&self) -> f64 {
        todo!("площадь прямоугольника")
    }
    fn perimeter(&self) -> f64 {
        todo!("периметр прямоугольника")
    }
}

// TODO 2: Circle (используйте PI): area = PI * r * r, perimeter = 2 * PI * r, name = "circle".
impl Shape for Circle {
    fn area(&self) -> f64 {
        todo!("площадь круга")
    }
    fn perimeter(&self) -> f64 {
        todo!("периметр круга")
    }
}

// TODO 3: Display для Rect в формате "Rect(2x3)": числа печатаются через {} (2.0 -> 2, 2.5 -> 2.5).
impl fmt::Display for Rect {
    fn fmt(&self, _f: &mut fmt::Formatter<'_>) -> fmt::Result {
        todo!("Display для Rect")
    }
}

// Сумма площадей. Подсказка: shapes.iter().map(|s| s.area()).sum()
pub fn total_area(_shapes: &[Box<dyn Shape>]) -> f64 {
    todo!("total_area")
}

// Фигура с наибольшей площадью (при равенстве - первая); None для пустого среза.
pub fn largest<'a>(_shapes: &'a [Box<dyn Shape>]) -> Option<&'a dyn Shape> {
    todo!("largest")
}

// Наибольший элемент среза; None для пустого. Подберите ограничения: T нужно сравнивать и копировать.
pub fn max_of<T: PartialOrd + Copy>(_xs: &[T]) -> Option<T> {
    todo!("max_of")
}

// Деньги в копейках.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Money(pub i64);

// TODO 4: Add, Neg, Mul<i64>, Sum и Display для Money.
//   Display: "19.99", "0.05", "-0.05" (знак, рубли, ровно две цифры копеек).
impl Add for Money {
    type Output = Money;
    fn add(self, _o: Money) -> Money {
        todo!("сложение")
    }
}

impl Neg for Money {
    type Output = Money;
    fn neg(self) -> Money {
        todo!("смена знака")
    }
}

impl Mul<i64> for Money {
    type Output = Money;
    fn mul(self, _k: i64) -> Money {
        todo!("умножение на число")
    }
}

impl Sum for Money {
    fn sum<I: Iterator<Item = Money>>(_iter: I) -> Money {
        todo!("сумма")
    }
}

impl fmt::Display for Money {
    fn fmt(&self, _f: &mut fmt::Formatter<'_>) -> fmt::Result {
        todo!("Display для Money")
    }
}

fn main() {
    // Здесь можно пробовать свой код кнопкой «Запустить»; проверка запускает отдельный файл.
    println!("{:?}", Money(1999));
}
