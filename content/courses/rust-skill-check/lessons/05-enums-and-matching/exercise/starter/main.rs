enum Shape {
    Circle(f64),
    Rect { w: f64, h: f64 },
    Square(f64),
}

// "circle R", "rect W H" или "square S"; размеры - конечные числа больше нуля.
fn parse_shape(line: &str) -> Option<Shape> {
    // TODO: разбейте строку на слова и сопоставьте первое слово с вариантом
    None
}

// Название варианта: "circle", "rect" или "square".
fn name(shape: &Shape) -> &'static str {
    // TODO: match shape { ... } со всеми тремя вариантами
    "?"
}

// Площадь фигуры (для круга - PI * r * r).
fn area(shape: &Shape) -> f64 {
    // TODO: match shape { ... }
    0.0
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut bad = 0;
    for line in input.lines() {
        match parse_shape(line) {
            Some(shape) => println!("{} {:.2}", name(&shape), area(&shape)),
            None => bad += 1,
        }
    }
    println!("bad={}", bad);
}
