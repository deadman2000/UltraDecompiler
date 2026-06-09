#include <stdio.h>

int counter = 0;

void bump(void)
{
    counter++;
}

int main(void)
{
    bump();
    bump();
    printf("%d\n", counter);

    return 0;
}
