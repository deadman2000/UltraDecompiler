#include <stdio.h>
#include <setjmp.h>

jmp_buf env;

int main(void)
{
    if (setjmp(env) == 0) {
        printf("first\n");
        longjmp(env, 1);
    }

    printf("second\n");

    return 0;
}
