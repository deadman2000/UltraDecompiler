#include <stdio.h>
#include <dos.h>

int main(void)
{
    int before = 1;
    struct dosdate_t d;
    int after = 2;

    (void)before;
    (void)after;

    _dos_getdate(&d);
    printf("%u/%u/%u\n", d.month, d.day, d.year);

    return 0;
}
