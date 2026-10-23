enum Shape {
    Circle(f64),
    Rect { w: f64, h: f64 },
    Square(f64),
}

fn positive(text: &str) -> Option<f64> {
    let value: f64 = text.parse().ok()?;
    if value.is_finite() && value > 0.0 {
        Some(value)
    } else {
        None
    }
}

fn parse_shape(line: &str) -> Option<Shape> {
    let words: Vec<&str> = line.split_whitespace().collect();
    match words.first().copied() {
        Some("circle") if words.len() == 2 => Some(Shape::Circle(positive(words[1])?)),
        Some("rect") if words.len() == 3 => Some(Shape::Rect {
            w: positive(words[1])?,
            h: positive(words[2])?,
        }),
        Some("square") if words.len() == 2 => Some(Shape::Square(positive(words[1])?)),
        _ => None,
    }
}

fn name(shape: &Shape) -> &'static str {
    match shape {
        Shape::Circle(_) => "circle",
        Shape::Rect { .. } => "rect",
        Shape::Square(_) => "square",
    }
}

fn area(shape: &Shape) -> f64 {
    match shape {
        Shape::Circle(r) => std::f64::consts::PI * r * r,
        Shape::Rect { w, h } => w * h,
        Shape::Square(s) => s * s,
    }
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
