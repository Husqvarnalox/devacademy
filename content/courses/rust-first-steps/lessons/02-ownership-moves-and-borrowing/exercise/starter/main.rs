use std::io::Read;

// TODO: функция только читает слова - принимайте их по ссылке
fn total_len(words: Vec<String>) -> usize {
    words.iter().map(|w| w.chars().count()).sum()
}

fn longest(words: &[String]) -> &str {
    let mut best = &words[0];
    for w in words {
        if w.chars().count() > best.chars().count() {
            best = w;
        }
    }
    best
}

fn shout(words: &mut Vec<String>) {
    for w in words.iter_mut() {
        *w = w.to_uppercase();
    }
}

fn main() {
    let mut input = String::new();
    std::io::stdin().read_to_string(&mut input).unwrap();
    let mut it = input.split_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let mut words: Vec<String> = it.take(n).map(String::from).collect();

    println!("total={}", total_len(words));
    println!("longest={}", longest(&words));
    shout(&mut words);
    println!("words={}", words.join(" "));
}
