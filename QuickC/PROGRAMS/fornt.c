#include <stdio.h>

int main(void)
{
    int i;
    int j;
    int product = 1;

    for (i = 1; i <= 3; i++) {
        for (j = 1; j <= 3; j++) {
            product += i * j;
        }
    }

    printf("nested for: %d\n", product);
    return 0;
}