#include <stdio.h>

struct Point {
    short x;
    short y;
};

int main(void)
{
    struct Point p;

    p.x = 10;
    p.y = 20;
    printf("%d %d\n", p.x, p.y);

    return 0;
}
