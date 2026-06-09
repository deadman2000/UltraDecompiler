#include <stdio.h>

int main(void)
{
    char far *vid = (char far *)0xB8000000L;
    unsigned i;

    for (i = 0; i < 80; i++) {
        vid[i * 2] = 'X';
    }

    printf("done\n");

    return 0;
}
