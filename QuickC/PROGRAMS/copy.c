#include <stdio.h>

void copy(char *dst, char *src)
{
    while (*src) {
        *dst++ = *src++;
    }
    *dst = 0;
}

void copy2(char *dst, char *src)
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
    int a = 10;
    char buf2[30];
    int b = 8;

    copy(buf, "test");
    copy(buf2, "test3");
    copy2(buf2, "test3");
    printf("%s\n", buf);

    return 0;
}
