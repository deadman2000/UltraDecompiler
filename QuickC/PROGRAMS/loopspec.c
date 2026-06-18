#include <stdio.h>

/* Простой for с ++ в обновлении (unopt/ opt) */
int for_inc(void) {
    int sum;
    int i;
    sum = 0;
    for (i = 0; i < 6; i++) {
        sum += i;
    }
    return sum;
}

/* for с шагом += 3 */
int for_step3(void) {
    int sum;
    int i;
    sum = 0;
    for (i = 0; i < 12; i += 3) {
        sum += i;
    }
    return sum;
}

/* for с обновлением умножением i *= 2 (или i = i*2) */
int for_mul(void) {
    int prod;
    int i;
    prod = 1;
    for (i = 1; i < 16; i = i * 2) {
        prod += i;
    }
    return prod;
}

/* for без выражения обновления (обновление внутри тела) */
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

/* for с несколькими переменными (init и update, запятая) */
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

/* for с шагом из переменной */
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

/* while с предусловием */
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

/* do-while */
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

/* бесконечный цикл с break */
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

/* for с пустым телом */
int for_empty_body(void) {
    int i;
    for (i = 0; i < 4; i++) ;
    return i;
}

/* вложенные циклы */
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

int main(void) {
    int r;
    /* вызываем, чтобы компилятор не выкинул функции как dead code */
    r = for_inc() + for_step3() + for_mul() + for_no_update_expr()
      + for_multi_var() + for_var_step() + while_pre() + do_while_post()
      + while_true_break() + for_empty_body() + nested_for();
    printf("%d\n", r);
    return 0;
}
