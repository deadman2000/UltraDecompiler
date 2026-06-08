#include <stdio.h>

short add(short a, short b)
{
	return a + b;
}

int main(void)
{
    short c = add(10, 5);
	
	printf("%d", c);
    
    return 0;
}