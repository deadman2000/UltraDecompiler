#include <stdio.h>

int main(void)
{
    int n = 0;
    int sum = 0;

    while (n < 10) {
        n++;
        if (n == 5) {
            continue;
        }
        sum += n;
    }

    printf("while continue: %d\n", sum);
    return 0;
}