#include <stdio.h>
#include <stdlib.h>

int main(void)
{
    char *p = malloc(32);

    if (p) {
        p[0] = 'A';
        p[1] = 0;
        printf("%s\n", p);
        free(p);
    }

    return 0;
}
