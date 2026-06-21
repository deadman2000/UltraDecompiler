#include <stdio.h>

int for_step3(void) {
    int sum;
    int i;
    sum = 0;
    for (i = 0; i < 12; i += 3) {
        sum += i;
    }
    return sum;
}

int for_mul(void) {
    int prod;
    int i;
    prod = 1;
    for (i = 1; i < 16; i = i * 2) {
        prod += i;
    }
    return prod;
}

int for_no_update_expr(void) {
    int sum;
    int i;
    sum = 0;
    for (i = 0; i < 5; ) {
        sum += i;
        i++;
    }
    return sum;
}

int for_multi_var(void) {
    int sum;
    int i;
    int j;
    sum = 0;
    for (i = 0, j = 10; i < j; i++, j--) {
        sum += i + j;
    }
    return sum;
}

int for_var_step(void) {
    int sum;
    int step;
    int i;
    sum = 0;
    step = 4;
    for (i = 1; i < 20; i += step) {
        sum += i;
    }
    return sum;
}

int while_pre(void) {
    int sum;
    int n;
    sum = 0;
    n = 7;
    while (n > 0) {
        sum += n;
        n--;
    }
    return sum;
}

int do_while_post(void) {
    int sum;
    int n;
    sum = 0;
    n = 4;
    do {
        sum += n;
        n--;
    } while (n > 0);
    return sum;
}

int while_true_break(void) {
    int count;
    count = 0;
    while (1) {
        count++;
        if (count >= 5) {
            break;
        }
    }
    return count;
}

int for_empty_body(void) {
    int i;
    for (i = 0; i < 4; i++) ;
    return i;
}

int nested_for(void) {
    int sum;
    int i;
    int j;
    sum = 0;
    for (i = 0; i < 3; i++) {
        for (j = 0; j < 3; j++) {
            sum += i * j;
        }
    }
    return sum;
}

int for_break(int N) {
    int i, sum;
    sum = 0;
    for (i = 0; i < N; i++) {
        if (i == 7)
            break;
        sum += i;
    }
    return sum;
}

int for_continue(int N) {
    int i, sum;
    sum = 0;
    for (i = 0; i < N; i++) {
        if ((i & 1) == 0)
            continue;
        sum += i;
    }
    return sum;
}

int while_break(int N) {
    int i, sum;
    sum = 0;
    i = 0;
    while (1) {
        if (i >= N)
            break;
        sum += i;
        i++;
    }
    return sum;
}

int while_continue(int N) {
    int i, sum;
    sum = 0;
    i = 0;
    while (i < N) {
        i++;
        if ((i & 1) == 0)
            continue;
        sum += i;
    }
    return sum;
}

int main(void) {
    int r = for_step3() + for_mul() + for_no_update_expr()
      + for_multi_var() + for_var_step() + while_pre() + do_while_post()
      + while_true_break() + for_empty_body() + nested_for()
      + for_break(10) + for_continue(10) + while_break(10) + while_continue(10);
    printf("%d\n", r);
    return 0;
}
