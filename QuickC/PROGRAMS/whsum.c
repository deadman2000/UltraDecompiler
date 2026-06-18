#include <stdio.h>

int main(void)
{
    int n = 5;
    int sum = 0;

    while (n > 0) {
        sum += n;
        n--;
    }

    printf("while sum: %d\n", sum);
    return 0;
}