#include <stdio.h>

enum Color {
    RED = 1,
    GREEN = 2,
    BLUE = 4
};

int main(void)
{
    enum Color c = GREEN;

    if (c == GREEN) {
        printf("green\n");
    }

    return 0;
}
