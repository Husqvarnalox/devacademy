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
        let root = Rc::new(Node {
            name: root.to_string(),
            parent: RefCell::new(Weak::new()),
            children: RefCell::new(Vec::new()),
        });
        let mut index = HashMap::new();
        index.insert(root.name.clone(), Rc::downgrade(&root));
        Tree { root, index }
    }

    fn find(&self, name: &str) -> Option<Rc<Node>> {
        self.index.get(name)?.upgrade()
    }

    fn add(&mut self, parent: &str, child: &str) -> Result<(), String> {
        let p = self.find(parent).ok_or(format!("unknown parent {parent}"))?;
        if self.find(child).is_some() {
            return Err(format!("duplicate {child}"));
        }
        let node = Rc::new(Node {
            name: child.to_string(),
            parent: RefCell::new(Rc::downgrade(&p)),
            children: RefCell::new(Vec::new()),
        });
        self.index.insert(child.to_string(), Rc::downgrade(&node));
        p.children.borrow_mut().push(node);
        Ok(())
    }

    fn path(&self, name: &str) -> Result<String, String> {
        let mut cur = self.find(name).ok_or(format!("unknown {name}"))?;
        let mut parts = vec![cur.name.clone()];
        loop {
            let next = cur.parent.borrow().upgrade();
            match next {
                Some(p) => {
                    parts.push(p.name.clone());
                    cur = p;
                }
                None => break,
            }
        }
        parts.reverse();
        Ok(parts.join("/"))
    }

    fn children(&self, name: &str) -> Result<Vec<String>, String> {
        let n = self.find(name).ok_or(format!("unknown {name}"))?;
        let names = n.children.borrow().iter().map(|c| c.name.clone()).collect();
        Ok(names)
    }

    fn remove(&mut self, name: &str) -> Result<usize, String> {
        let n = self.find(name).ok_or(format!("unknown {name}"))?;
        let parent = n.parent.borrow().upgrade().ok_or("cannot remove root".to_string())?;
        parent.children.borrow_mut().retain(|c| !Rc::ptr_eq(c, &n));
        Ok(size(&n))
    }

    fn count(&self) -> usize {
        self.index.values().filter(|w| w.upgrade().is_some()).count()
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
