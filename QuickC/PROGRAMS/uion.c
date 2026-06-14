#include <stdio.h>

union Value {
    unsigned short word;
    unsigned char bytes[2];
};

int main(void)
{
    union Value v;

    v.word = 0x1234;
    printf("%u %u %u\n", v.word, v.bytes[0], v.bytes[1]);

    return 0;
}
