// Забирает строку во владение и возвращает её в верхнем регистре с "!" в конце.
fn shout(text: String) -> String {
    // TODO: to_uppercase() и push('!')
    text
}

// Возвращает более длинную строку (по len()); при равенстве - первую.
fn longest<'a>(a: &'a str, b: &'a str) -> &'a str {
    // TODO: сравните a.len() и b.len()
    a
}

// Дописывает suffix в конец каждой строки вектора.
fn add_suffix(names: &mut Vec<String>, suffix: &str) {
    // TODO: for name in names.iter_mut() { name.push_str(suffix); }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    for line in input.lines() {
        let words: Vec<&str> = line.split_whitespace().collect();
        if words.is_empty() {
            continue;
        }
        match words[0] {
            "shout" => println!("{}", shout(words[1].to_string())),
            "longest" => println!("{}", longest(words[1], words[2])),
            "suffix" => {
                let mut names: Vec<String> = words[2..].iter().map(|w| w.to_string()).collect();
                add_suffix(&mut names, words[1]);
                println!("{}", names.join(" "));
            }
            _ => {}
        }
    }
}
