CC     := qcl
CFLAGS := /nologo /AS /Gs

SRCS := $(wildcard *.c)

EXES := $(SRCS:.c=.exe)

.PHONY: all clean

all: $(EXES)
	@echo Сборка завершена.

%.exe: %.c
	$(CC) $(CFLAGS) $< /Fe$@

clean:
	del *.obj
	del *.exe