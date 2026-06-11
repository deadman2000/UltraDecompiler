CC     := qcl
CFLAGS := /nologo /AS /Gs /Od
LIBS   := SLIBCE.LIB LIBH.LIB

SRCS := $(wildcard *.c)

EXES := $(SRCS:.c=.exe)

.PHONY: all clean

all: $(EXES)
	@echo Сборка завершена.

%.exe: %.c
	$(CC) $(CFLAGS) $< /Fe$@ $(LIBS)

clean:
	del *.obj
	del *.exe