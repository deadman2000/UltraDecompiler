#include <stdio.h>

int main(void)
{
    int n = 3;
    int acc = 0;

    do {
        acc += n;
        n--;
    } while (n > 0);

    printf("%d\n", acc);

    return 0;
}
