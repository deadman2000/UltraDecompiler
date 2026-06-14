#include <stdio.h>

int add(int a, int b)
{
    return a + b;
}

int sub(int a, int b)
{
    return a - b;
}

int main(void)
{
    int (*op)(int, int);
    int x;

    op = add;
    x = op(7, 3);
    printf("%d\n", x);

    op = sub;
    x = op(7, 3);
    printf("%d\n", x);

    return 0;
}
