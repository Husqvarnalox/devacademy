use std::cell::RefCell;
use std::collections::HashMap;
use std::io::{self, BufRead};
use std::rc::{Rc, Weak};

struct Node {
    name: String,
    parent: RefCell<Weak<Node>>,
    children: RefCell<Vec<Rc<Node>>>,
}

struct Tree {
    root: Rc<Node>,
    // Индекс хранит только слабые ссылки: он не продлевает жизнь узлов.
    index: HashMap<String, Weak<Node>>,
}

impl Tree {
    fn new(root: &str) -> Tree {
        todo!("создайте корень Rc<Node> и индекс с Rc::downgrade")
    }

    fn find(&self, name: &str) -> Option<Rc<Node>> {
        todo!("index.get(name)?.upgrade()")
    }

    fn add(&mut self, parent: &str, child: &str) -> Result<(), String> {
        todo!("найти родителя, проверить дубликат, создать узел с Weak на родителя и положить Rc в children родителя")
    }

    fn path(&self, name: &str) -> Result<String, String> {
        todo!("идти вверх по parent.upgrade() и собрать имена")
    }

    fn children(&self, name: &str) -> Result<Vec<String>, String> {
        todo!("имена из children.borrow()")
    }

    fn remove(&mut self, name: &str) -> Result<usize, String> {
        todo!("retain с Rc::ptr_eq в children родителя; вернуть размер поддерева")
    }

    fn count(&self) -> usize {
        todo!("считать живые Weak в индексе")
    }
}

fn size(n: &Rc<Node>) -> usize {
    1 + n.children.borrow().iter().map(size).sum::<usize>()
}

fn main() {
    let stdin = io::stdin();
    let mut tree: Option<Tree> = None;
    for line in stdin.lock().lines() {
        let line = line.unwrap();
        let parts: Vec<&str> = line.split_whitespace().collect();
        let Some(&cmd) = parts.first() else { continue };
        if cmd == "root" {
            tree = Some(Tree::new(parts[1]));
            continue;
        }
        let Some(t) = tree.as_mut() else {
            println!("error: no root");
            continue;
        };
        let res: Result<String, String> = match (cmd, &parts[1..]) {
            ("add", [p, c]) => t.add(p, c).map(|_| String::new()),
            ("path", [n]) => t.path(n),
            ("children", [n]) => t.children(n).map(|v| {
                if v.is_empty() { "(none)".to_string() } else { v.join(" ") }
            }),
            ("remove", [n]) => t.remove(n).map(|k| format!("removed {k}")),
            ("count", []) => Ok(t.count().to_string()),
            _ => Err(format!("bad command {cmd}")),
        };
        match res {
            Ok(s) if s.is_empty() => {}
            Ok(s) => println!("{s}"),
            Err(e) => println!("error: {e}"),
        }
    }
    if let Some(t) = &tree {
        println!("strong(root)={} weak(root)={}", Rc::strong_count(&t.root), Rc::weak_count(&t.root));
    }
}
