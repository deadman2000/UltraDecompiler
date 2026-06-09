#include <stdio.h>
#include <dos.h>

int main(void)
{
    struct dosdate_t d;

    _dos_getdate(&d);
    printf("%u/%u/%u\n", d.month, d.day, d.year);

    return 0;
}
