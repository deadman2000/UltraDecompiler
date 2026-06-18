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

int sum_while(void)
{
    int n = 5;
    int sum = 0;

    while (n > 0) {
        sum += n;
        n--;
    }

    return sum;
}

void copy_str(char *dst, char *src)
{
    while (*src) {
        *dst++ = *src++;
    }
    *dst = 0;
}

int nested_for(void)
{
    int i;
    int j;
    int product = 1;

    for (i = 1; i <= 3; i++) {
        for (j = 1; j <= 3; j++) {
            product += i * j;
        }
    }

    return product;
}

int main(void)
{
    char buf[16];

    printf("for sum: %d\n", sum_for());
    printf("for countdown: %d\n", countdown_for());
    printf("while sum: %d\n", sum_while());
    printf("nested for: %d\n", nested_for());

    copy_str(buf, "loops");
    printf("while copy: %s\n", buf);

    return 0;
}