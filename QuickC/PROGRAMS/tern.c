#include <stdio.h>

int pick(int a, int b, int use_a)
{
    return use_a ? a : b;
}

int main(void)
{
    printf("%d %d\n", pick(10, 20, 1), pick(10, 20, 0));

    return 0;
}
