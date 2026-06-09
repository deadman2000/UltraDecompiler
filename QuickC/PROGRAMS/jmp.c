#include <stdio.h>

int main(void)
{
    int x = 0;

    goto start;
start:
    x = 42;
    if (x > 0) {
        goto done;
    }
    x = 0;
done:
    printf("%d\n", x);

    return 0;
}
