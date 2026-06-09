#include <stdio.h>

int main(void)
{
    char far *screen = (char far *)0xB8000000L;

    *screen = 'A';
    printf("ok\n");

    return 0;
}
