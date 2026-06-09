#include <stdio.h>

unsigned rol(unsigned x, int n)
{
    return (x << n) | (x >> (16 - n));
}

int main(void)
{
    printf("%u\n", rol(0x8001, 3));

    return 0;
}
