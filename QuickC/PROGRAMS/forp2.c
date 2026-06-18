#include <stdio.h>

int main(void)
{
    int i;
    int sum = 0;

    for (i = 0; i < 10; i += 2) {
        sum += i;
    }

    printf("step2: %d\n", sum);
    return 0;
}