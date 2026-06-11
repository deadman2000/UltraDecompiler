#include <stdio.h>

void copy(char *dst, char *src)
{
    while (*src) {
        *dst = *src;
        dst++;
        src++;
    }
    *dst = 0;
}

int main(void)
{
    char buf[20];

    copy(buf, "test");
    printf("%s\n", buf);

    return 0;
}
