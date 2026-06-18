#include <stdio.h>

int sum_for(void)
{
    int i;
    int sum = 0;

    for (i = 0; i < 5; i++) {
        sum += i;
    }

    return sum;
}

int countdown_for(void)
{
    int i;
    int acc = 0;

    for (i = 3; i > 0; i--) {
        acc += i;
    }

    return acc;
}

int main(void)
{
    printf("for sum: %d\n", sum_for());
    printf("for countdown: %d\n", countdown_for());
    return 0;
}