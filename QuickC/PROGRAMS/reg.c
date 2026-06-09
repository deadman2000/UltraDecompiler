#include <stdio.h>

int main(void)
{
    register int r;

    r = 0;
    while (r < 5) {
        r++;
    }
    printf("%d\n", r);

    return 0;
}
