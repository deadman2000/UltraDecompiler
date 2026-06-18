#include <stdio.h>

int main(void)
{
    int i;
    int sum = 0;

    for (i = 0; i < 10; i++) {
        if (i & 1) {
            continue;
        }
        sum += i;
    }

    printf("for continue: %d\n", sum);
    return 0;
}