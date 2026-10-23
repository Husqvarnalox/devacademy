fn count_words(text: &str) -> i32 {
    // TODO: посчитайте слова через split_whitespace
    0
}

fn acronym(text: &str) -> String {
    // TODO: склейте первые буквы слов и переведите в верхний регистр
    String::new()
}

fn longest_word(text: &str) -> Option<String> {
    // TODO: найдите самое длинное слово (по символам), первое из равных
    None
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
