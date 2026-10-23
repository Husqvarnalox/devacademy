class Product(val name: String, val price: Int) {
    // TODO: метод cost(count: Int): Int - стоимость count штук (price * count)
    fun cost(count: Int): Int {
        return 0
    }

    // TODO: метод describe(count: Int): String - строка вида "название: count x price = стоимость"
    fun describe(count: Int): String {
        return ""
    }
}

fun main() {
    val n = readln().toInt()
    var total = 0
    for (i in 1..n) {
        val parts = readln().split(" ")
        val product = Product(parts[0], parts[1].toInt())
        val count = parts[2].toInt()
        println(product.describe(count))
        total += product.cost(count)
    }
    println("Итого: $total")
}
