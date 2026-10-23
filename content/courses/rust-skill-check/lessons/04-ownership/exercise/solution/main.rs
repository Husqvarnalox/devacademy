fn shout(text: String) -> String {
    let mut result = text.to_uppercase();
    result.push('!');
    result
}

fn longest<'a>(a: &'a str, b: &'a str) -> &'a str {
    if b.len() > a.len() { b } else { a }
}

fn add_suffix(names: &mut Vec<String>, suffix: &str) {
    for name in names.iter_mut() {
        name.push_str(suffix);
    }
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
