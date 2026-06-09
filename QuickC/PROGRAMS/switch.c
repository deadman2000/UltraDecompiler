#include <stdio.h>

int main(void)
{
    int x = 3;

    switch (x) {
    case 1:
        printf("one\n");
        break;
    case 2:
        printf("two\n");
        break;
    case 3:
        printf("three\n");
        break;
    default:
        printf("other\n");
        break;
    }

    return 0;
}
