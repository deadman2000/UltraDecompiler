#include <stdio.h>

struct Flags {
    unsigned int ready : 1;
    unsigned int mode : 3;
    unsigned int count : 4;
};

int main(void)
{
    struct Flags f;

    f.ready = 1;
    f.mode = 5;
    f.count = 10;
    printf("%u %u %u\n", f.ready, f.mode, f.count);

    return 0;
}
