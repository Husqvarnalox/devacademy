use std::collections::HashMap;

fn add_vote(votes: &mut HashMap<String, i32>, name: &str) {
    // TODO: добавьте один голос за name
}

fn winner(votes: &HashMap<String, i32>) -> Option<String> {
    // TODO: верните имя с наибольшим числом голосов (при равенстве - раньше по алфавиту)
    None
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut votes: HashMap<String, i32> = HashMap::new();
    for line in input.lines() {
        let name = line.trim();
        if name != "" {
            add_vote(&mut votes, name);
        }
    }

    let mut names: Vec<&String> = Vec::new();
    for name in votes.keys() {
        names.push(name);
    }
    names.sort();
    for name in names {
        println!("{name}: {}", votes[name]);
    }

    match winner(&votes) {
        Some(name) => println!("Победитель: {name}"),
        None => println!("Победитель: нет"),
    }
}
