fn grade_letter(score: i32) -> char {
    match score {
        90..=100 => 'A',
        80..=89 => 'B',
        70..=79 => 'C',
        60..=69 => 'D',
        0..=59 => 'F',
        _ => '?',
    }
}

fn digit_sum(n: u32) -> u32 {
    let mut rest = n;
    let mut sum = 0;
    while rest > 0 {
        sum += rest % 10;
        rest /= 10;
    }
    sum
}

fn fahrenheit(celsius: f64) -> f64 {
    celsius * 9.0 / 5.0 + 32.0
}

fn low_byte(n: i32) -> u8 {
    n as u8
}

fn clamp_add(a: i32, b: i32) -> i32 {
    a.saturating_add(b)
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
