struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn new(name: &str, score: i32) -> Student {
        // TODO: создайте студента из имени и балла
        Student { name: String::new(), score: 0 }
    }

    fn is_passed(&self) -> bool {
        // TODO: true, если балл 60 и выше
        false
    }

    fn label(&self) -> String {
        // TODO: отлично (90+), хорошо (75+), зачёт (60+), незачёт
        String::new()
    }

    fn describe(&self) -> String {
        // TODO: "<имя>: <балл> (<оценка словами>)"
        String::new()
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut students: Vec<Student> = Vec::new();
    let mut skipped = 0;
    for line in input.lines() {
        match line.split_once(';') {
            Some((name, score_text)) => match score_text.trim().parse::<i32>() {
                Ok(score) => students.push(Student::new(name.trim(), score)),
                Err(_) => skipped += 1,
            },
            None => skipped += 1,
        }
    }
    for s in &students {
        println!("{}", s.describe());
    }
    let mut passed = 0;
    for s in &students {
        if s.is_passed() {
            passed += 1;
        }
    }
    println!("Сдали: {passed} из {}, пропущено строк: {skipped}", students.len());
}
