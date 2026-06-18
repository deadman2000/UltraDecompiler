#include <stdio.h>

void copy_str(char *dst, char *src)
{
    while (*src) {
        *dst++ = *src++;
    }
    *dst = 0;
}

int main(void)
{
    char buf[16];

    copy_str(buf, "loops");
    printf("while copy: %s\n", buf);
    return 0;
}