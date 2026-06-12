void foo(int flag)
{
	if (flag)
	{
	}
}

void foo_ret(int flag)
{
	if (flag)
	{
		return;
	}
	return;
}

int main(void)
{
	foo(1);
	foo_ret(1);
    return 0;
}
