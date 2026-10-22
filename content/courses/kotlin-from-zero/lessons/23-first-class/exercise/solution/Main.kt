class Product(val name: String, val price: Int) {
    fun cost(count: Int): Int {
        return price * count
    }

    fun describe(count: Int): String {
        return "$name: $count x $price = ${cost(count)}"
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
