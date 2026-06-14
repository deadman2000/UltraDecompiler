#include <stdio.h>

int main(void)
{
    int x = 3;
	
    if (x != 1)
    {
        if (x != 2)
        {
            if (x != 3)
            {
                printf("other\n");
            }
            else
            {
                printf("three\n");
            }
        }
        else
        {
            printf("two\n");
        }
    }
    else
    {
        printf("one\n");
    }

    switch (x) {
    case 1:
        printf("one\n");
        break;
    case 2:
        printf("two\n");
        break;
    case 3:
        printf("three\n");
        break;
    default:
        printf("other\n");
        break;
    }

    return 0;
}
