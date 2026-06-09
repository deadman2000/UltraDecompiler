#include <stdio.h>
#include <conio.h>

int main(void)
{
    int v = inp(0x60);

    outp(0x61, v & 0xFD);
    printf("%d\n", v);

    return 0;
}
