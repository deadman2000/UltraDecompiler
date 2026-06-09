#include <stdio.h>
#include <string.h>

int main(void)
{
    char buf[16];

    strcpy(buf, "hello");
    printf("%s\n", buf);

    return 0;
}
