#include <stdio.h>

int main(void)
{
    unsigned char b = *((unsigned char far *)0xB8000000L);

    printf("%u\n", b);

    return 0;
}
