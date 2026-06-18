#include <stdio.h>

int main(void)
{
    int i;
    int sum = 0;

    for (i = 0; i < 100; i++) {
        if (i == 7) {
            break;
        }
        sum += i;
    }

    printf("for break: %d\n", sum);
    return 0;
}