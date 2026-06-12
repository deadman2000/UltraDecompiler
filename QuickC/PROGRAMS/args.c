#include <stdio.h>

static int is_flag(const char *s, char letter)
{
    return s[0] == '-' && s[1] == letter && s[2] == '\0';
}

int main(int argc, char *argv[])
{
    int i, verbose = 0, count = 0;
    if (argc < 2) {
        printf("usage: %s [-v] <word>...\n", argv[0]);
        return 1;
    }
    for (i = 1; i < argc; i++) {
        if (is_flag(argv[i], 'h')) {
            printf("usage: %s [-v] <word>...\n", argv[0]);
            return 0;
        }
        if (is_flag(argv[i], 'v')) {
            verbose = 1;
            continue;
        }
        count++;
        if (verbose)
            printf("[%d] ", count);
        printf("%s\n", argv[i]);
    }
    printf("total: %d\n", count);
    return 0;
}
