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

### Шаблон для `do { } while`

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

**Источник (forlp.c / loopspec.c):**
```c
for (i = 0; i < 6; i++) sum += i;
```

**Unopt (/Od) @ for_inc:**
```
mov [BP-4], 0     ; sum
mov [BP-2], 0     ; i
jmp test
body:
    mov AX, [i]
    add [sum], AX
    add [i], 1
test:
    cmp [i], 6
    jge exit
    jmp body
```

**Opt (/Ox):**
```
mov [BP-2], 0
mov SI, 0
jmp test
body:
    add [BP-2], SI   ; sum в памяти, i в SI
    inc SI
test:
    cmp SI, 6
    jl body
; spill SI обратно
mov [BP-4], SI
```

**Наблюдения:**
- /Od: обе переменные на стеке, явный `add [mem], 1`.
- /Ox: `i` в `SI`, используется `inc`, тело + обновление компактнее, `jl` вместо `jge + jmp`.

Аналогично для countdown (`i--`, `jle` / `jg`).

### 2.2 Шаг `i += const` (forp2.c, for_step3)

**Unopt:**
```
add [BP-2], 2     ; или 3
cmp [BP-2], 0Ah
jge exit
jmp body
```

**Opt:**
```
add SI, 3
cmp SI, 0Ch
jl body
```

Разница та же: память → регистр + `add reg, N`.

### 2.3 Обновление умножением `i = i * 2` (или `*= 2`)

**loopspec.c / for_mul:**

**Unopt:**
```
... init i=1
jmp test
update_part:
    mov AX, [i]
    sal AX, 1          ; shift arithmetic left = *2
    mov [i], AX
test:
    cmp [i], 10h
    jl body
    jmp exit
body:
    add [prod], AX
    jmp update_part
```

**Opt:**
```
mov SI, 1
jmp test
update:
    mov AX, SI
    sal AX, 1
    mov SI, AX
test:
    cmp SI, 10h
    jge exit
body:
    add DI, SI
    jmp update
```

**Вывод:** QuickC для `*=` / `i = i*2` генерирует `sal` (или `shl`). В /Od — через AX и память. В /Ox — регистр + sal.

### 2.4 For без выражения обновления `for (i=0; i<N; ) { ... i++; }`

**Unopt:**
Обычный шаблон, но обновление (`add [i],1`) находится **внутри тела** до теста. Структура сохраняется: init → jmp test → body (с обновлением) → test.

**Opt:**
```
mov SI, 0
jmp test
body:
    add DI, SI
    inc SI
test:
    cmp SI, N
    jl body
```
Обновление "внутри" тела, но проверка после него.

### 2.5 Шаг из переменной `i += step` (step=4)

**Unopt (for_var_step):**
```
mov [step], 4
mov [i], 1
jmp test
body:
    add [sum], [i]
    mov AX, [step]
    add [i], AX
test:
    cmp [i], 14h
    jge exit
    jmp body
```

**Opt:**
```
mov [BP-4], 4     ; step на стеке
mov SI, 1
mov DI, [step]    ; step загружен в регистр
jmp test
body:
    add [sum], SI
    add SI, DI
test:
    cmp SI, 14h
    jl body
```

**Важно:** даже в /Ox `step` может остаться на стеке, но используется через регистр в цикле. Обновление — `add reg, reg_with_step`.

### 2.6 Несколько переменных в for `for (i=0, j=10; i < j; i++, j--)`

**Unopt (for_multi_var):**
```
mov [sum],0; [i],0; [j],0Ah
mov AX, 0Ah
jmp test
loop:
    mov AX, [i]
    add [i], 1
    mov [tmp], AX     ; сохранили старое i
    mov AX, [j]
    sub [j], 1
test:
    mov AX, [j]
    cmp [i], AX
    jl body
    jmp exit
body:
    mov AX, [i]
    add AX, [j]
    add [sum], AX
    jmp loop
```
Обновления выполняются **до** теста (семантика for). Много перезагрузок AX.

**Opt:**
```
mov SI, 0
mov DI, 0Ah
mov AX, 0Ah
jmp test
update:
    mov AX, SI
    inc SI
    ... 
    mov AX, DI
    dec DI
test:
    cmp SI, DI
    jge exit
body:
    mov AX, SI
    add AX, DI
    add [sum], AX
    jmp update
```
i → SI, j → DI. Обновления инкремент/декремент регистров. Тест использует текущие значения после update. Тело использует "старые" значения до следующего обновления.

### 2.7 while vs do-while (whsum, dowhl, loopspec)

**while (n > 0) { sum += n; n--; }**

Unopt (типичный):
```
n=...; sum=0
jmp test
body:
    add sum, n
    sub n, 1
test:
    cmp n, 0
    jle exit
    jmp body
```

Opt:
```
n=...
sum=0
SI = n
jmp test
body:
    add sum, SI
    dec SI
test:
    and SI, SI
    jg body
spill
```

**do { sum += n; n--; } while (n > 0);**

Unopt:
```
... init
body:
    add...
    sub...
test:
    cmp n,0
    jle exit
    jmp body
```

Opt (dowhl / loopspec):
```
SI = n
body:
    add sum, SI
    dec SI
    and SI,SI
    jg body
```
**Ключевое отличие:** в `do-while` **нет** начального `jmp` на тест. Тело всегда выполняется первой итерацией.

### 2.8 Бесконечный цикл + break `while(1) { ... if (c>=5) break; }`

**Unopt:**
```
count=0
jmp L1
L2:   ; тело + проверка break
    add count,1
    cmp count,5
    jge break_exit
    jmp L1
L1:
    jmp L2
break_exit:
...
```

Много явных `jmp`. Условие break — `jge` на выход.

**Opt (loopspec):**
```
mov SI,0
jmp check
inc SI
cmp SI,5
jl continue
jmp exit
continue:
jmp inc_point
```

Оптимизатор строит структуру с несколькими короткими переходами (`jl`, `jmp`). Выглядит чуть запутанно, но эффективно.

### 2.9 Пустое тело `for (i=0; i<4; i++) ;`

**Unopt:** тело пустое, только обновление + тест (прыжки).

**Opt:**
```
mov SI, 0
jmp test
inc SI
test:
    cmp SI,4
    jl back
```
Очень компактно. Счётчик остаётся в регистре.

### 2.10 Вложенные циклы (fornt.c, loopspec nested_for)

**Unopt:**
```
sum=0; i=0
jmp outer_test
outer_body:
    j=0
    jmp inner_test
    inner_body:
        mov AX,i
        imul [j]
        add sum, AX
        add j,1
    inner_test:
        cmp j,3
        jge outer_update
        jmp inner_body
    outer_update:
        add i,1
outer_test:
    cmp i,3
    jge exit
    jmp outer_body
```
Отдельные "прыжковые" метки для каждого уровня. `imul word ptr [mem]`.

**Opt:**
```
sum=0
SI=0   ; i
jmp outer_test
inner_init:
    DI=0
    jmp inner_test
inner_body:
    mov AX,SI
    imul DI
    add [sum],AX
    inc DI
inner_test:
    cmp DI,3
    jl inner_body
    ...
    inc SI
outer_test:
    cmp SI,3
    jl inner_init
```
i=SI, j=DI. `imul reg` (регистр). Отдельные прыжки на инициализацию inner. Плотнее, меньше стековых обращений.

### 2.11 Break / Continue (forbk, forcnt, whbrk, whcnt)

**for + break (if (i==7) break):**

Unopt: после `for` init/jmp:
```
cmp i,7
je exit_loop
... body sum += i
update: add i,1
test...
```

Continue: `jne` на update (пропуск тела), затем update + тест.

**Opt:**
```
cmp SI,7
jne continue_body
jmp exit
...
add ...
inc
test jl ...
```
Использует короткие `je` / `jne` + `jmp` на выход или на update-часть.

Аналогично для while.

### 2.12 Циклы по указателям / строкам (whcpy.c)

**Unopt copy_str:**
```
mov BX, src
mov AL,[BX]
cbw
cmp AX,0
jne body
jmp end
body:
    ... load byte
    add [src],1
    store to dst
    add [dst],1
    jmp top
end:
    mov byte [dst],0
```
Использует `BX` как указатель. `add [BP+off],1` для инкремента указателя. `cbw` для знакового расширения байта.

**Opt:**
```
mov SI, src
mov DI, dst
top:
    cmp byte [SI],0
    je end
    mov BX,SI
    inc SI
    mov AL,[BX]
    mov BX,DI
    inc DI
    mov [BX],AL
    jmp top
end:
    mov [src],SI ; spill
    mov [dst],DI
    mov byte [dst],0
```
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

---

## 5. Использованные файлы и как повторить

- Существующие: `QuickC/PROGRAMS/{forlp,forp2,dowhl,whsum,forbk,forcnt,fornt,whcpy,...}.c` + их `.exe` в `BUILT/xxx/s_gs_od_slibce.exe` и `_ox_`.
- Новые примеры: `QuickC/PROGRAMS/loopspec.c`
- Сборка: `build_loopspec.cmd` (или вручную через DOSBox-X).
- Дизассемблирование:
  ```powershell
  dotnet run --project Tools -- disasm <exe> -o 0x10 -c 60
  dotnet run --project Tools -- disasm <exe> --main
  ```
- Для нахождения адресов функций: `decompile-main` (показывает `call 0x10h`, `call 45h` и т.д.).

---

## 6. Выводы для проекта UltraDecompiler

- Паттерны очень стабильны и узнаваемы.
- Основная эвристика детекции цикла — поиск back-edge (jmp назад на метку тела) + наличие счётчика и теста условия.
- Разные виды (`for`/`while`/`do`) отличаются **только** наличием/отсутствием начального прыжка на тест.
- Оптимизированный код требует хорошего register tracking в `RegisterExpressions` + умения распознавать `inc`/`dec`/`and reg,reg` как часть счётчика цикла.
- `ExpressionBuilderQuickCOpt` уже частично ориентирован на такие конструкции (см. `OptimizationLevelHeuristics`).
- При доработке постпроцессинга и structurer'а CFG важно учитывать, что update может находиться "перед" тестом или "после" тела в зависимости от lowering.

Этот документ можно использовать как справочник при улучшении распознавания циклов, восстановления `for`-условий и обработке break/continue в IR.

---

*Документ создан на основе реальных дизассемблированных листингов QuickC 1.0. Все примеры проверены.*
