CC     := qcl
CFLAGS := /nologo

SRCS := $(wildcard *.c)

EXES_S := $(SRCS:.c=_s.exe)
EXES_M := $(SRCS:.c=_m.exe)
EXES_C := $(SRCS:.c=_c.exe)
EXES_L := $(SRCS:.c=_l.exe)

ALL_EXES := $(EXES_S) $(EXES_M) $(EXES_C) $(EXES_L)

.PHONY: all clean

all: $(ALL_EXES)
	@echo Сборка завершена.
# ==================== Small ====================
%_s.exe: %.c
	$(CC) /AS $(CFLAGS) $< /Fe$@

# ==================== Medium ====================
%_m.exe: %.c
	$(CC) /AM $(CFLAGS) $< /Fe$@

# ==================== Compact ====================
%_c.exe: %.c
	$(CC) /AC $(CFLAGS) $< /Fe$@

# ==================== Large ====================
%_l.exe: %.c
	$(CC) /AL $(CFLAGS) $< /Fe$@

clean:
	del *.obj
	del *.exe