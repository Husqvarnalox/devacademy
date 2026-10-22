struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn new(name: &str, score: i32) -> Student {
        Student {
            name: String::from(name),
            score,
        }
    }

    fn is_passed(&self) -> bool {
        self.score >= 60
    }

    fn label(&self) -> String {
        if self.score >= 90 {
            String::from("отлично")
        } else if self.score >= 75 {
            String::from("хорошо")
        } else if self.score >= 60 {
            String::from("зачёт")
        } else {
            String::from("незачёт")
        }
    }

    fn describe(&self) -> String {
        format!("{}: {} ({})", self.name, self.score, self.label())
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
