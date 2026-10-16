a, b, c = map(int, input().split())
if min(a, b, c) <= 0 or a + b <= c or a + c <= b or b + c <= a:
    print("invalid")
elif a == b == c:
    print("equilateral")
elif a == b or b == c or a == c:
    print("isosceles")
else:
    print("scalene")
