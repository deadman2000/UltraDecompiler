#include <stdio.h>
#include <dos.h>

int main(void)
{
    char far *vid = MK_FP(0xB800, 0);
    unsigned i;

    for (i = 0; i < 80; i++) {
        vid[i * 2] = 'X';
    }

    printf("done\n");

    return 0;
}
