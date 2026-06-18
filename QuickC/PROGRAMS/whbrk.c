#include <stdio.h>

int main(void)
{
    int n = 0;
    int sum = 0;

    while (n < 50) {
        if (n == 12) {
            break;
        }
        sum += n;
        n++;
    }

    printf("while break: %d\n", sum);
    return 0;
}