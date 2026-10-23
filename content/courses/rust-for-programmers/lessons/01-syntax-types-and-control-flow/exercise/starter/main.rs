fn grade_letter(score: i32) -> char {
    // TODO: верните 'A' для 90..=100, 'B' для 80..=89, 'C' для 70..=79,
    // 'D' для 60..=69, 'F' для 0..=59 и '?' для остальных значений
    '-'
}

fn digit_sum(n: u32) -> u32 {
    // TODO: верните сумму десятичных цифр числа
    0
}

fn fahrenheit(celsius: f64) -> f64 {
    // TODO: переведите градусы Цельсия в Фаренгейта
    0.0
}

fn low_byte(n: i32) -> u8 {
    // TODO: верните младший байт числа
    0
}

fn clamp_add(a: i32, b: i32) -> i32 {
    // TODO: верните a + b, а при переполнении - ближайшую границу i32
    0
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    for line in input.lines() {
        let mut parts = line.split_whitespace();
        let command = parts.next().unwrap_or("");
        let first = parts.next().unwrap_or("0");
        let second = parts.next().unwrap_or("0");
        match command {
            "grade" => println!("{}", grade_letter(first.parse().unwrap())),
            "digits" => println!("{}", digit_sum(first.parse().unwrap())),
            "temp" => println!("{:.1}", fahrenheit(first.parse().unwrap())),
            "byte" => println!("{}", low_byte(first.parse().unwrap())),
            "add" => println!("{}", clamp_add(first.parse().unwrap(), second.parse().unwrap())),
            _ => {}
        }
    }
}
