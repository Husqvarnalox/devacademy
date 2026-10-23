use std::collections::HashMap;

fn add_vote(votes: &mut HashMap<String, i32>, name: &str) {
    *votes.entry(String::from(name)).or_insert(0) += 1;
}

fn winner(votes: &HashMap<String, i32>) -> Option<String> {
    let mut best_name = String::new();
    let mut best_count = 0;
    for (name, count) in votes {
        if *count > best_count || (*count == best_count && *name < best_name) {
            best_name = name.clone();
            best_count = *count;
        }
    }
    if best_count == 0 {
        None
    } else {
        Some(best_name)
    }
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
