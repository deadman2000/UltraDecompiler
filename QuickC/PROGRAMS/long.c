#include <stdio.h>

long mix(long a, long b)
{
    long sum;
    long diff;
    long prod;
    long quot;
    long rem;
    long shifted;

    sum = a + b;
    diff = a - b;
    prod = a * b;
    quot = a / b;
    rem = a % b;
    shifted = (a << 4) + (b >> 2);

    return sum + diff + prod + quot + rem + shifted;
}

int main(void)
{
    printf("%ld\n", mix(0x1234L, 0x5678L));

    return 0;
}
