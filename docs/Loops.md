# Анализ компиляции циклов в Microsoft QuickC 1.0

**Дата анализа:** 2026  
**Цель:** Изучить, в какой ассемблерный код (8086, real mode, small model) превращаются разные виды циклов C (`for`, `while`, `do-while`) при компиляции QuickC 1.0 с флагами `/Od` (без оптимизаций) и `/Ox` (максимальная оптимизация).  
**Использованные источники:**
- Примеры из `QuickC/PROGRAMS/` (forlp.c, dowhl.c, whsum.c, forp2.c, forbk.c, forcnt.c, whbrk.c, whcpy.c, fornt.c и др.)
- Собственные примеры в `QuickC/PROGRAMS/loopspec.c` (специально скомпилированы)
- Дизассемблирование через встроенный дизассемблер проекта (`udc disasm`) + построенные EXE (`/Od` и `/Ox`, модель small `/AS`, `/Gs`)
- Сравнение `s_gs_od_slibce.exe` и `s_gs_ox_slibce.exe` из `QuickC/BUILT/`

Все примеры собраны с помощью `QCL /nologo /AS /Gs /Od|Ox ... SLIBCE.LIB`.

---

## Как обновить IR-деревья в этом документе

```cmd
.\generate-all-loop-ir-graphs.cmd
```

---

## 1. Общая модель генерации циклов QuickC

QuickC следует классической схеме понижения циклов в `if` + `goto` (с проверкой условия в конце тела или в начале).

### Шаблон для `for` и `while` (предусловие)

**Unoptimized (/Od):**
```
init;
jmp test;
body:
    ... тело ...
update:
    ... обновление ...
test:
    cmp ...
    jcc exit
    jmp body
exit:
```

- Все переменные — на стеке `[BP-offset]`.
- Инициализация перед прыжком.
- Проверка условия вынесена после обновления (или в "тест").
- Для выхода используется `jge`/`jle`/`jae` и т.п. + безусловный `jmp` обратно.
- Обновление (`i++`, `i+=2`, `i--`) — отдельно после тела.

**Optimized (/Ox):**
```
init (часто в регистр);
jmp test;
body:
    ... тело (счётчик уже в регистре) ...
update:
    inc / add / dec ...
test:
    cmp / and reg,reg
    jl / jg / jne ... body
exit:
    (опционально spill регистров обратно в [BP])
```

- Счётчики итераций и вспомогательные значения — в **регистрах** (`SI`, `DI`, иногда `BX`).
- Меньше стековых обращений.
- `inc`/`dec` вместо `add 1` / `sub 1`.
- Часто `and SI,SI ; jg` вместо `cmp SI,0 ; jg`.
- Прыжки назад короче (относительные).
- В конце функции часто сохраняют значения регистров обратно в стековые слоты (видимо, для совместимости/отладки).
- Тело и обновление часто "слиты" (обновление выполняется перед проверкой или сразу после).

### Шаблон для do-while

**Unopt:**
```
init;
body:
    ...
update:
    ...
test:
    cmp ...
    jcc body   ; если условие истинно — повторить
exit:
```

Тело гарантированно выполняется минимум один раз. Нет начального `jmp`.

**Opt:** почти идентично, но тело/обновление использует регистры, `and reg,reg + jg` и т.д. Начального прыжка на тест **нет**.

---

## 2. Конкретные примеры и различия

### 2.1 Простой `for (i = 0; i < N; i++)`

**Источник: forlp.c (sum_for)**

```c
int sum_for(void)
{
    int i, sum;
    sum = 0;
    for (i = 0; i < 5; i++)
        sum += i;
    return sum;
}
```

**Unopt (/Od) @ sum_for:**
```asm
; Инициализация
mov     [BP-2], 0           ; i = 0
mov     [BP-4], 0           ; sum = 0
jmp     test_label          ; прыжок на проверку условия

; Тело цикла
body_label:
    mov     AX, [BP-2]              ; загрузить i
    add     [BP-4], AX              ; sum += i
    add     [BP-2], 1               ; i++

; Проверка условия
test_label:
    cmp     [BP-2], 5               ; сравнить i с 5
    jge     exit_label              ; если i >= 5, выход из цикла
    jmp     body_label              ; иначе — повторить

exit_label:
    ; код после цикла
```

![sum_for /Od](loops-ir-graphs/sum_for_Od.png)

**Opt (/Ox):**
```asm
; Инициализация (счётчик в регистре)
mov     SI, 0               ; i = 0 (в SI)
mov     [BP-4], 0           ; sum = 0
jmp     test_label

; Тело цикла
body_label:
    add     [BP-4], SI              ; sum += i (из регистра)
    inc     SI                      ; i++ (короткая инструкция)

; Проверка условия
test_label:
    cmp     SI, 5                   ; сравнить регистр с константой
    jl      body_label              ; если SI < 5, повторить

; Spill регистра обратно в стек
mov     [BP-2], SI          ; сохранить i
```

![sum_for /Ox](loops-ir-graphs/sum_for_Ox.png)

**Наблюдения:**
- /Od: обе переменные на стеке, явный `add [mem], 1`.
- /Ox: `i` в `SI`, используется `inc`, тело + обновление компактнее, `jl` вместо `jge + jmp`.
- На IR-дереве /Od видны три основных блока: инициализация, тело цикла (с `add [sum], AX`), проверка условия с переходом.
- На IR-дереве /Ox заметна оптимизация: счётчик в регистре `SI`, операция `inc SI` вместо `add [mem], 1`.

Аналогично для countdown (`i--`, `jle` / `jg`) — функция `countdown_for` в forlp.c.

### 2.2 Шаг `i += const` (forp2.c, for_step3)

**Источник: forp2.c, loopspec.c (for_step3)**

```c
int for_step3(void)
{
    int i, sum;
    sum = 0;
    for (i = 0; i < 12; i += 3)
        sum += i;
    return sum;
}
```

**Unopt:**
```asm
; Тело цикла + обновление
body_label:
    add     [BP-2], 3               ; i += 3
    cmp     [BP-2], 0Ch             ; сравнить i с 12
    jge     exit_label              ; если i >= 12, выход
    jmp     body_label              ; повторить
```

![for_step3 /Od](loops-ir-graphs/for_step3_Od.png)

**Opt:**
```asm
; Тело цикла + обновление (в регистре)
body_label:
    add     SI, 3                   ; i += 3 (в регистре)
    cmp     SI, 0Ch                 ; сравнить SI с 12
    jl      body_label              ; если SI < 12, повторить
```

![for_step3 /Ox](loops-ir-graphs/for_step3_Ox.png)

Разница та же: память → регистр + `add reg, N`.

### 2.3 Обновление умножением `i = i * 2` (или `*= 2`)

**Источник: loopspec.c (for_mul)**

```c
int for_mul(void)
{
    int i, prod;
    prod = 0;
    for (i = 1; i < 16; i *= 2)
        prod += i;
    return prod;
}
```

**Unopt:**
```asm
; Инициализация
; ... init i = 1
jmp     test_label

; Обновление (умножение на 2 через сдвиг)
update_label:
    mov     AX, [BP-2]              ; загрузить i
    sal     AX, 1                   ; AX = i * 2 (сдвиг влево)
    mov     [BP-2], AX              ; сохранить i

; Проверка условия
test_label:
    cmp     [BP-2], 10h             ; сравнить i с 16
    jl      body_label              ; если i < 16, продолжить
    jmp     exit_label              ; иначе выход

; Тело цикла
body_label:
    add     [BP-4], AX              ; prod += i
    jmp     update_label            ; перейти к обновлению
```

![for_mul /Od](loops-ir-graphs/for_mul_Od.png)

**Opt:**
```asm
; Инициализация
mov     SI, 1               ; i = 1 (в SI)
jmp     test_label

; Обновление
update_label:
    mov     AX, SI                  ; скопировать i
    sal     AX, 1                   ; AX = i * 2
    mov     SI, AX                  ; сохранить в SI

; Проверка условия
test_label:
    cmp     SI, 10h                 ; сравнить SI с 16
    jge     exit_label              ; если SI >= 16, выход

; Тело цикла
body_label:
    add     DI, SI                  ; prod += i (DI = prod)
    jmp     update_label            ; перейти к обновлению
```

![for_mul /Ox](loops-ir-graphs/for_mul_Ox.png)

**Вывод:** QuickC для `*=` / `i = i*2` генерирует `sal` (или `shl`). В /Od — через AX и память. В /Ox — регистр + sal.

### 2.4 For без выражения обновления `for (i=0; i<N; ) { ... i++; }`

**Источник: loopspec.c (for_no_update)**

```c
int for_no_update(void)
{
    int i, sum;
    sum = 0;
    for (i = 0; i < 6; )
    {
        sum += i;
        i++;
    }
    return sum;
}
```

**Unopt:**
```asm
; Инициализация
mov     [BP-4], 0           ; sum = 0
mov     [BP-2], 0           ; i = 0
jmp     test_label

; Тело цикла (обновление внутри тела)
body_label:
    mov     AX, [BP-2]              ; загрузить i
    add     [BP-4], AX              ; sum += i
    add     [BP-2], 1               ; i++ (обновление в теле)

; Проверка условия
test_label:
    cmp     [BP-2], 6               ; сравнить i с 6
    jge     exit_label              ; если i >= 6, выход
    jmp     body_label              ; иначе повторить
```

![for_no_update /Od](loops-ir-graphs/for_no_update_Od.png)

**Opt:**
```asm
; Инициализация (в регистры)
mov     SI, 0               ; i = 0 (в SI)
jmp     test_label

; Тело цикла
body_label:
    add     DI, SI                  ; sum += i (DI = sum)
    inc     SI                      ; i++

; Проверка условия
test_label:
    cmp     SI, N                   ; сравнить SI с N
    jl      body_label              ; если SI < N, повторить
```

![for_no_update /Ox](loops-ir-graphs/for_no_update_Ox.png)

Обновление "внутри" тела, но проверка после него.

### 2.5 Шаг из переменной `i += step` (step=4)

**Источник (loopspec.c / for_var_step):**

```c
int for_var_step(void)
{
    int i, step, sum;
    step = 4;
    sum = 0;
    for (i = 1; i < 20; i += step)
        sum += i;
    return sum;
}
```

**Unopt (for_var_step):**
```asm
; Инициализация
mov     [BP-4], 4           ; step = 4
mov     [BP-2], 1           ; i = 1
jmp     test_label

; Тело цикла
body_label:
    add     [BP-6], [BP-2]          ; sum += i
    mov     AX, [BP-4]              ; загрузить step
    add     [BP-2], AX              ; i += step

; Проверка условия
test_label:
    cmp     [BP-2], 14h             ; сравнить i с 20
    jge     exit_label              ; если i >= 20, выход
    jmp     body_label              ; иначе повторить
```

![for_var_step /Od](loops-ir-graphs/for_var_step_Od.png)

**Opt:**
```asm
; Инициализация
mov     [BP-4], 4           ; step на стеке
mov     SI, 1               ; i = 1 (в SI)
mov     DI, [BP-4]          ; step в DI
jmp     test_label

; Тело цикла
body_label:
    add     [BP-6], SI              ; sum += i
    add     SI, DI                  ; i += step (регистр + регистр)

; Проверка условия
test_label:
    cmp     SI, 14h                 ; сравнить SI с 20
    jl      body_label              ; если SI < 20, повторить
```

![for_var_step /Ox](loops-ir-graphs/for_var_step_Ox.png)

**Важно:** даже в /Ox `step` может остаться на стеке, но используется через регистр в цикле. Обновление — `add reg, reg_with_step`.

### 2.6 Несколько переменных в for `for (i=0, j=10; i < j; i++, j--)`

**Источник (loopspec.c / for_multi_var):**

```c
int for_multi_var(void)
{
    int i, j, sum;
    sum = 0;
    for (i = 0, j = 10; i < j; i++, j--)
        sum += (i + j);
    return sum;
}
```

**Unopt (for_multi_var):**
```asm
; Инициализация
mov     [BP-6], 0           ; sum = 0
mov     [BP-4], 0           ; i = 0
mov     [BP-2], 0Ah         ; j = 10
jmp     test_label

; Обновление переменных (перед тестом, семантика for)
update_label:
    mov     AX, [BP-4]              ; загрузить i
    add     [BP-4], 1               ; i++
    mov     [BP-8], AX              ; сохранить старое i во временную
    mov     AX, [BP-2]              ; загрузить j
    sub     [BP-2], 1               ; j--

; Проверка условия
test_label:
    mov     AX, [BP-2]              ; загрузить j
    cmp     [BP-4], AX              ; сравнить i с j
    jl      body_label              ; если i < j, выполнить тело
    jmp     exit_label              ; иначе выход

; Тело цикла
body_label:
    mov     AX, [BP-4]              ; загрузить i
    add     AX, [BP-2]              ; AX = i + j
    add     [BP-6], AX              ; sum += (i + j)
    jmp     update_label            ; перейти к обновлению
```
Обновления выполняются **до** теста (семантика for). Много перезагрузок AX.

![for_multi_var /Od](loops-ir-graphs/for_multi_var_Od.png)

**Opt:**
```asm
; Инициализация (в регистры)
mov     SI, 0               ; i = 0 (в SI)
mov     DI, 0Ah             ; j = 10 (в DI)
jmp     test_label

; Обновление переменных
update_label:
    mov     AX, SI                  ; сохранить старое i
    inc     SI                      ; i++
    mov     AX, DI                  ; сохранить старое j
    dec     DI                      ; j--

; Проверка условия
test_label:
    cmp     SI, DI                  ; сравнить i с j
    jge     exit_label              ; если i >= j, выход

; Тело цикла
body_label:
    mov     AX, SI                  ; загрузить i
    add     AX, DI                  ; AX = i + j
    add     [BP-6], AX              ; sum += (i + j)
    jmp     update_label            ; перейти к обновлению
```

![for_multi_var /Ox](loops-ir-graphs/for_multi_var_Ox.png)

i → SI, j → DI. Обновления инкремент/декремент регистров. Тест использует текущие значения после update. Тело использует "старые" значения до следующего обновления.

### 2.7 while vs do-while (whsum, dowhl, loopspec)

**while (n > 0) { sum += n; n--; }**

```c
int while_pre(int n)
{
    int sum;
    sum = 0;
    while (n > 0)
    {
        sum += n;
        n--;
    }
    return sum;
}
```

Unopt (типичный):
```asm
; Инициализация
mov     [BP-2], N           ; n = ...
mov     [BP-4], 0           ; sum = 0
jmp     test_label          ; прыжок на проверку (предусловие)

; Тело цикла
body_label:
    mov     AX, [BP-2]              ; загрузить n
    add     [BP-4], AX              ; sum += n
    sub     [BP-2], 1               ; n--

; Проверка условия
test_label:
    cmp     [BP-2], 0               ; сравнить n с 0
    jle     exit_label              ; если n <= 0, выход
    jmp     body_label              ; иначе повторить
```

![while_pre /Od](loops-ir-graphs/while_pre_Od.png)

Opt:
```asm
; Инициализация (в регистр)
mov     [BP-2], N           ; n = ...
mov     [BP-4], 0           ; sum = 0
mov     SI, [BP-2]          ; n в SI
jmp     test_label

; Тело цикла
body_label:
    add     [BP-4], SI              ; sum += n
    dec     SI                      ; n--

; Проверка условия
test_label:
    and     SI, SI                  ; проверка на ноль (быстрее cmp)
    jg      body_label              ; если SI > 0, повторить

; Spill
mov     [BP-2], SI          ; сохранить n обратно
```

![while_pre /Ox](loops-ir-graphs/while_pre_Ox.png)

**do { sum += n; n--; } while (n > 0);**

```c
int do_while(int n)
{
    int sum;
    sum = 0;
    do
    {
        sum += n;
        n--;
    } while (n > 0);
    return sum;
}
```

Unopt:
```asm
; Инициализация
mov     [BP-2], N           ; n = ...
mov     [BP-4], 0           ; sum = 0

; Тело цикла (выполняется минимум 1 раз, нет начального jmp)
body_label:
    mov     AX, [BP-2]              ; загрузить n
    add     [BP-4], AX              ; sum += n
    sub     [BP-2], 1               ; n--

; Проверка условия (после тела)
test_label:
    cmp     [BP-2], 0               ; сравнить n с 0
    jle     exit_label              ; если n <= 0, выход
    jmp     body_label              ; иначе повторить
```

![do_while /Od](loops-ir-graphs/do_while_Od.png)

Opt (dowhl / loopspec):
```asm
; Инициализация (в регистр)
mov     SI, N               ; n в SI

; Тело цикла + проверка (слиты)
body_label:
    add     [BP-4], SI              ; sum += n
    dec     SI                      ; n--
    and     SI, SI                  ; проверка на ноль
    jg      body_label              ; если SI > 0, повторить
```

![do_while /Ox](loops-ir-graphs/do_while_Ox.png)

**Ключевое отличие:** в `do-while` **нет** начального `jmp` на тест. Тело всегда выполняется первой итерацией.

**Наблюдения по IR:**
- На IR-дереве `while` виден начальный блок инициализации, затем прыжок на проверку условия (`jmp test`).
- В `do-while` тело следует сразу за инициализацией — это ключевое структурное различие.

### 2.8 Бесконечный цикл + break `while(1) { ... if (c>=5) break; }`

**Источник (loopspec.c / while_break):**

```c
int while_break(void)
{
    int count;
    count = 0;
    while (1)
    {
        count++;
        if (count >= 5)
            break;
    }
    return count;
}
```

**Unopt:**
```asm
; Инициализация
mov     [BP-2], 0           ; count = 0
jmp     body_label          ; прыжок на тело

; Проверка break
check_break_label:
    cmp     [BP-2], 5               ; сравнить count с 5
    jge     exit_label              ; если count >= 5, выход (break)
    jmp     body_label              ; иначе повторить

; Тело цикла
body_label:
    add     [BP-2], 1               ; count++
    cmp     [BP-2], 5               ; проверить условие break
    jge     check_break_label       ; перейти к проверке
    jmp     body_label              ; (избыточный jmp в /Od)

exit_label:
    ; код после цикла
```

![while_break /Od](loops-ir-graphs/while_break_Od.png)

Много явных `jmp`. Условие break — `jge` на выход.

**Opt (loopspec):**
```asm
; Инициализация
mov     SI, 0               ; count в SI
jmp     check_label

; Тело + проверка
inc_label:
    inc     SI                      ; count++
    cmp     SI, 5                   ; сравнить с 5
    jl      continue_label          ; если SI < 5, продолжить
    jmp     exit_label              ; иначе выход

continue_label:
    jmp     inc_label               ; повторить

check_label:
    jmp     inc_label               ; первый вход

exit_label:
    ; код после цикла
```

![while_break /Ox](loops-ir-graphs/while_break_Ox.png)

Оптимизатор строит структуру с несколькими короткими переходами (`jl`, `jmp`). Выглядит чуть запутанно, но эффективно.

### 2.9 Пустое тело `for (i=0; i<4; i++) ;`

**Источник (loopspec.c / for_empty):**

```c
int for_empty(void)
{
    int i;
    for (i = 0; i < 4; i++)
        ; /* пустое тело */
    return i;
}
```

**Unopt:** тело пустое, только обновление + тест (прыжки).

![for_empty /Od](loops-ir-graphs/for_empty_Od.png)

**Opt:**
```
mov SI, 0
jmp test
inc SI
test:
    cmp SI,4
    jl back
```

![for_empty /Ox](loops-ir-graphs/for_empty_Ox.png)

Очень компактно. Счётчик остаётся в регистре.

### 2.10 Вложенные циклы (fornt.c, loopspec nested_for)

**Источник: fornt.c, loopspec.c (nested_for)**

```c
int nested_for(void)
{
    int i, j, sum;
    sum = 0;
    for (i = 0; i < 3; i++)
    {
        for (j = 0; j < 3; j++)
            sum += (i * j);
    }
    return sum;
}
```

**Unopt:**
```asm
; Инициализация внешнего цикла
mov     [BP-6], 0           ; sum = 0
mov     [BP-2], 0           ; i = 0
jmp     outer_test_label

; Внешний цикл
outer_body_label:
    ; Инициализация внутреннего цикла
    mov     [BP-4], 0               ; j = 0
    jmp     inner_test_label

    ; Внутренний цикл
    inner_body_label:
        mov     AX, [BP-2]                  ; загрузить i
        imul    [BP-4]                      ; AX = i * j
        add     [BP-6], AX                  ; sum += AX
        add     [BP-4], 1                   ; j++

    ; Проверка внутреннего цикла
    inner_test_label:
        cmp     [BP-4], 3                   ; сравнить j с 3
        jge     outer_update_label          ; если j >= 3, выход из внутреннего
        jmp     inner_body_label            ; иначе повторить внутренний

    ; Обновление внешнего цикла
    outer_update_label:
        add     [BP-2], 1                   ; i++

; Проверка внешнего цикла
outer_test_label:
    cmp     [BP-2], 3                       ; сравнить i с 3
    jge     exit_label                      ; если i >= 3, выход
    jmp     outer_body_label                ; иначе повторить внешний

exit_label:
    ; код после циклов
```
Отдельные "прыжковые" метки для каждого уровня. `imul word ptr [mem]`.

![nested_for /Od](loops-ir-graphs/nested_for_Od.png)

**Opt:**
```asm
; Инициализация (в регистры)
mov     [BP-6], 0           ; sum = 0
mov     SI, 0               ; i в SI
jmp     outer_test_label

; Внешний цикл
outer_body_label:
    ; Инициализация внутреннего цикла
    mov     DI, 0                   ; j в DI
    jmp     inner_test_label

    ; Внутренний цикл
    inner_body_label:
        mov     AX, SI                      ; AX = i
        imul    DI                          ; AX = i * j (регистр)
        add     [BP-6], AX                  ; sum += AX
        inc     DI                          ; j++

    ; Проверка внутреннего цикла
    inner_test_label:
        cmp     DI, 3                       ; сравнить DI с 3
        jl      inner_body_label            ; если DI < 3, повторить
        jmp     outer_update_label          ; иначе выход

    ; Обновление внешнего цикла
    outer_update_label:
        inc     SI                          ; i++

; Проверка внешнего цикла
outer_test_label:
    cmp     SI, 3                           ; сравнить SI с 3
    jl      outer_body_label                ; если SI < 3, повторить

exit_label:
    ; код после циклов
```

![nested_for /Ox](loops-ir-graphs/nested_for_Ox.png)

i=SI, j=DI. `imul reg` (регистр). Отдельные прыжки на инициализацию inner. Плотнее, меньше стековых обращений.

### 2.11 Break / Continue (forbk, forcnt, whbrk, whcnt)

**for + break (if (i==7) break):**

```c
int for_break(int N)
{
    int i, sum;
    sum = 0;
    for (i = 0; i < N; i++)
    {
        if (i == 7)
            break;
        sum += i;
    }
    return sum;
}

int for_continue(int N)
{
    int i, sum;
    sum = 0;
    for (i = 0; i < N; i++)
    {
        if ((i & 1) == 0)
            continue;
        sum += i;
    }
    return sum;
}

int while_break(int N)
{
    int i, sum;
    sum = 0;
    i = 0;
    while (1)
    {
        if (i >= N)
            break;
        sum += i;
        i++;
    }
    return sum;
}

int while_continue(int N)
{
    int i, sum;
    sum = 0;
    i = 0;
    while (i < N)
    {
        i++;
        if ((i & 1) == 0)
            continue;
        sum += i;
    }
    return sum;
}
```

**Unopt:** после `for` init/jmp:
```asm
; Инициализация for
mov     [BP-2], 0           ; i = 0
jmp     test_label

; Проверка break (в начале тела)
body_label:
    cmp     [BP-2], 7               ; сравнить i с 7
    je      exit_loop               ; если i == 7, выход (break)
    
    ; Тело: sum += i
    mov     AX, [BP-2]
    add     [BP-4], AX
    
    ; Обновление
    add     [BP-2], 1               ; i++

; Проверка условия
test_label:
    cmp     [BP-2], N               ; сравнить i с N
    jge     exit_loop               ; если i >= N, выход
    jmp     body_label              ; иначе повторить

exit_loop:
    ; код после цикла
```

![for_break /Od](loops-ir-graphs/for_break_Od.png)

Continue: `jne` на update (пропуск тела), затем update + тест.

**Opt:**
```asm
; Инициализация
mov     SI, 0               ; i в SI
jmp     test_label

; Проверка break
body_label:
    cmp     SI, 7                   ; сравнить SI с 7
    jne     continue_body           ; если SI != 7, выполнить тело
    jmp     exit_loop               ; break

continue_body:
    ; Тело: sum += SI
    add     [BP-4], SI
    inc     SI                      ; i++

; Проверка условия
test_label:
    cmp     SI, N                   ; сравнить SI с N
    jl      body_label              ; если SI < N, повторить

exit_loop:
    ; код после цикла
```
cmp SI,7
jne continue_body
jmp exit
...
add ...
inc
test jl ...
```

![for_break /Ox](loops-ir-graphs/for_break_Ox.png)

Continue (opt):
```asm
; Инициализация
mov     SI, 0               ; i в SI
jmp     test_label

; Проверка continue
body_label:
    mov     AX, SI                  ; загрузить i
    and     AX, 1                   ; i & 1
    jne     update_label            ; если нечётное, выполнить тело
    jmp     update_label            ; continue → пропуск тела

update_label:
    inc     SI                      ; i++

; Проверка условия
test_label:
    cmp     SI, N
    jl      body_label
```

![for_continue /Ox](loops-ir-graphs/for_continue_Ox.png)

Использует короткие `je` / `jne` + `jmp` на выход или на update-часть.

Аналогично для while.

### 2.12 Циклы по указателям / строкам (whcpy.c)

**Источник: whcpy.c (copy_str)**

```c
void copy_str(char *dst, const char *src)
{
    while (*src != '\0')
    {
        *dst++ = *src++;
    }
    *dst = '\0';
}
```

**Unopt copy_str:**
```asm
; Инициализация указателей
mov     BX, [BP+6]          ; BX = src

; Проверка условия (предусловие)
top_label:
    mov     AL, [BX]                ; AL = *src
    cbw                             ; знаковое расширение байта в AX
    cmp     AX, 0                   ; сравнить с 0
    jne     body_label              ; если *src != 0, выполнить тело
    jmp     exit_label              ; иначе выход

; Тело цикла: *dst++ = *src++
body_label:
    mov     BX, [BP+6]              ; загрузить src
    mov     AL, [BX]                ; AL = *src
    add     [BP+6], 1               ; src++
    
    mov     BX, [BP+4]              ; загрузить dst
    mov     [BX], AL                ; *dst = AL
    add     [BP+4], 1               ; dst++
    
    jmp     top_label               ; повторить

exit_label:
    mov     BX, [BP+4]              ; загрузить dst
    mov     byte ptr [BX], 0        ; *dst = '\0' (терминатор)
```
Использует `BX` как указатель. `add [BP+off],1` для инкремента указателя. `cbw` для знакового расширения байта.

![ptr_loop /Od](loops-ir-graphs/ptr_loop_Od.png)

**Opt:**
```asm
; Инициализация (указатели в регистры)
mov     SI, [BP+6]          ; src в SI
mov     DI, [BP+4]          ; dst в DI

; Тело цикла + проверка (слиты)
top_label:
    cmp     byte ptr [SI], 0        ; проверка *src
    je      exit_label              ; если *src == 0, выход
    
    ; Тело: *dst++ = *src++ (post-increment)
    mov     BX, SI                  ; BX = src
    inc     SI                      ; src++ (после чтения)
    mov     AL, [BX]                ; AL = *src
    mov     BX, DI                  ; BX = dst
    inc     DI                      ; dst++ (после записи)
    mov     [BX], AL                ; *dst = AL
    
    jmp     top_label               ; повторить

exit_label:
    mov     [BP+6], SI              ; spill src
    mov     [BP+4], DI              ; spill dst
    mov     byte ptr [DI], 0        ; *dst = '\0'
```

![ptr_loop /Ox](loops-ir-graphs/ptr_loop_Ox.png)

SI/DI — указатели. Инкремент **после** чтения (`mov BX,SI; inc SI; mov AL,[BX]` — это post-increment семантика `*dst++ = *src++`). `inc reg` вместо add. Spill в конце.

---

## 3. Сравнительная таблица /Od vs /Ox

| Аспект                    | /Od (без оптимизаций)                  | /Ox (максимальная)                          |
|---------------------------|----------------------------------------|---------------------------------------------|
| Местоположение счётчиков  | Только стек `[BP-x]`                   | Преимущественно регистры (SI, DI)           |
| Обновление i++            | `add [mem], 1`                         | `inc reg`                                   |
| Обновление i += k         | `add [mem], k`                         | `add reg, k`                                |
| Условие выхода            | `cmp [mem], N; jge + jmp`              | `cmp reg,N; jl` (или `and reg,reg; jg`)     |
| Кол-во прыжков            | Много (init → jmp test + тело → update → jmp) | Меньше, часто слитые update+test            |
| Вложенные циклы           | Полностью стек + отдельные jmp для каждого уровня | Регистры для i/j + imul reg                 |
| Pointer/byte loops        | BX + add [mem],1                       | SI/DI + inc reg + post-inc паттерн          |
| Spill в конце             | Редко (уже на стеке)                   | Часто (сохранение SI/DI обратно в [BP])     |
| Мёртвые присваивания      | Много (в т.ч. после return)            | Меньше                                      |
| Размер кода функции       | Больше                                 | Заметно компактнее                          |
| `do {}` vs `while {}`     | Отличаются наличием/отсутствием начального jmp | То же, но с регистровыми оптимизациями      |

---

## 4. Дополнительные наблюдения

1. **Регистровое распределение** — примитивное, но эффективное для простых счётчиков. Быстро отдаёт SI/DI. При нехватке регистров — spill.
2. **and reg,reg** — любимый трюк QuickC для теста "больше нуля" после dec (вместо `cmp reg, 0`).
3. **sal вместо mul** — для константных умножений на 2.
4. **Структура for** — даже при `i = i * 2` компилятор помещает обновление **перед** проверкой условия в шаблоне (семантика for).
5. **Продолжение после break/continue** — реализовано явными `jmp` на метку обновления или теста, без использования специальных инструкций.
6. **Влияние /Gs** — нет вызовов `__chkstk` в прологах (в примерах стека мало).
7. **Маленькие константы** — часто загружаются напрямую `mov reg, imm`.
8. **Выравнивание / порядок** — переменные размещаются в порядке объявления/использования, иногда с "мусором" (загрузка [BP] перед перезаписью константой в opt).
9. **ir в декомпиляторе проекта** — часто распознаёт такие циклы обратно в `for` даже из while-формы (благодаря TailReturnInserter / EpilogueAnalyzer и анализу back-edge).
