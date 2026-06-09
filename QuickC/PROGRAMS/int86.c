#include <stdio.h>
#include <dos.h>

int main(void)
{
    union REGS in, out;

    in.x.ax = 0x3000;
    int86(0x21, &in, &out);
    printf("%u\n", out.x.ax);

    return 0;
}
