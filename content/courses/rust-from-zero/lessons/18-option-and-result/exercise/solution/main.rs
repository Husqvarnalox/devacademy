fn parse_score(text: &str) -> Option<i32> {
    match text.trim().parse::<i32>() {
        Ok(n) => {
            if n >= 0 && n <= 100 {
                Some(n)
            } else {
                None
            }
        }
        Err(_) => None,
    }
}

fn describe(text: &str) -> String {
    match parse_score(text) {
        Some(score) => {
            if score >= 60 {
                format!("Балл {score}: зачёт")
            } else {
                format!("Балл {score}: незачёт")
            }
        }
        None => format!("Некорректный балл: {text}"),
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut valid = 0;
    for line in input.lines() {
        println!("{}", describe(line));
        if parse_score(line).is_some() {
            valid += 1;
        }
    }
    println!("Верных баллов: {valid}");
}
