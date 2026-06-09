#include <stdio.h>

long mix(long a, long b)
{
    return (a << 4) + (b >> 2);
}

int main(void)
{
    printf("%ld\n", mix(0x1234L, 0x5678L));

    return 0;
}
