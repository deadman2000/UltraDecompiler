#include <stdio.h>
#include <dos.h>

int main(void)
{
    char far *screen = MK_FP(0xB800, 0);

    *screen = 'A';
    printf("ok\n");

    return 0;
}
