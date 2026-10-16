a, b, c = map(int, input().split())
# TODO: сначала проверьте, что треугольник существует (стороны > 0 и каждая меньше суммы двух других)
# TODO: затем определите тип: equilateral, isosceles или scalene
if a == b == c:
    print("equilateral")
else:
    print("scalene")
