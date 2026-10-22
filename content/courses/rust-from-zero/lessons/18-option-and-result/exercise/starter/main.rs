fn parse_score(text: &str) -> Option<i32> {
    // TODO: разберите text.trim().parse::<i32>() и проверьте диапазон 0..=100
    None
}

fn describe(text: &str) -> String {
    // TODO: сформируйте строку по результату parse_score
    String::new()
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
