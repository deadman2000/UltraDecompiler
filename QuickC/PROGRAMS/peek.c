#include <stdio.h>
#include <dos.h>

int main(void)
{
    unsigned char b = peek(0xB800, 0);

    printf("%u\n", b);

    return 0;
}
