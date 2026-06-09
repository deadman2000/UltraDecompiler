#include <stdio.h>
#include <dos.h>

int main(void)
{
    _disable();
    printf("off\n");
    _enable();
    printf("on\n");

    return 0;
}
