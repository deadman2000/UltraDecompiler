#include <stdio.h>

int main(void)
{
    int a = 1;
    int b = 0;
    int c = 2;

    if (a && c) {
        printf("ac\n");
    }
    if (b || c) {
        printf("bc\n");
    }

    return 0;
}
