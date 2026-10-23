fn main() {
    // Эти две строки читают весь ввод и готовят построчное чтение. Менять их не нужно.
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();

    // TODO 1: прочитайте три строки ввода: получателя, город и описание посылки.
    // Каждую берите так: lines.next().unwrap().trim()
    let name = "";
    let city = "";
    let parcel = "";

    println!("Получатель: {name}");
    println!("Город: {}", city.to_uppercase());
    println!("Посылка: {parcel}");
    println!("Букв в описании: {}", parcel.chars().count());
}
