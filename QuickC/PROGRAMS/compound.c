int main(void)
{
    int a = 10;
    int b = 3;

    a = a + 5;
    a += 5;

    a = a - 5;
    a -= 5;

    a = a + b;
    a += b;

    a = a - b;
    a -= b;

    return a + b;
}