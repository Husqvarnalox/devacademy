fn count_words(text: &str) -> i32 {
    let mut n = 0;
    for _w in text.split_whitespace() {
        n += 1;
    }
    n
}

fn acronym(text: &str) -> String {
    let mut result = String::new();
    for word in text.split_whitespace() {
        if let Some(c) = word.chars().next() {
            result.push(c);
        }
    }
    result.to_uppercase()
}

fn longest_word(text: &str) -> Option<String> {
    let mut best = "";
    for word in text.split_whitespace() {
        if word.chars().count() > best.chars().count() {
            best = word;
        }
    }
    if best == "" {
        None
    } else {
        Some(String::from(best))
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let text = input.lines().next().unwrap_or("");
    println!("Слов: {}", count_words(text));
    println!("Аббревиатура: {}", acronym(text));
    match longest_word(text) {
        Some(w) => println!("Самое длинное: {w}"),
        None => println!("Самое длинное: нет"),
    }
}
