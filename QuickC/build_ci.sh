#!/usr/bin/env bash
#
# build_ci.sh — сборка эталонных .EXE QuickC через DOSBox (для Linux / GitHub Actions).
#
# Используется в CI, чтобы тесты (LibMatchingTests и др.) получали свежие
# скомпилированные примеры ПЕРЕД запуском dotnet test.
#
# Требования: dosbox-staging (предпочтительно) или dosbox в PATH.
# На Ubuntu: sudo apt-get install -y dosbox-staging
#
# Скрипт монтирует каталог QuickC как диск C: и запускает build_dos.bat внутри эмулятора.
# Полученные .EXE появляются в реальной файловой системе в QuickC/PROGRAMS/.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== Building QuickC reference examples via DOSBox ==="
echo "Working dir: $(pwd)"

# Выбираем команду dosbox
DOSBOX=""
if command -v dosbox-staging >/dev/null 2>&1; then
  DOSBOX="dosbox-staging"
elif command -v dosbox >/dev/null 2>&1; then
  DOSBOX="dosbox"
else
  echo "ERROR: neither dosbox-staging nor dosbox found in PATH."
  echo "On Ubuntu/Debian runner: sudo apt-get update && sudo apt-get install -y dosbox-staging"
  exit 1
fi

echo "Using: $DOSBOX"

# Запуск DOSBox в полностью автоматическом режиме.
# - mount c .     → текущая директория (QuickC) становится диском C:
# - Настраиваем переменные окружения как в оригинальных .cmd
# - Переходим в PROGRAMS и вызываем build_dos.bat
# - -exit в конце — закрыть DOSBox после выполнения

"$DOSBOX" \
  -c "mount c ." \
  -c "c:" \
  -c "config -set cpu cycles=30000" \
  -c "set LIB=c:\\" \
  -c "set INCLUDE=c:\\INCLUDE" \
  -c "set PATH=c:\\" \
  -c "cd PROGRAMS" \
  -c "call ..\\build_dos.bat" \
  -c "exit" \
  -exit

echo "=== DOSBox QuickC build completed ==="

echo
echo "Produced executables:"
ls -l PROGRAMS/*_?.EXE 2>/dev/null || echo "(no *_?.EXE files found — build may have failed)"
echo

# Verify at least the known files exist (non-fatal here, dotnet test will fail if missing)
for f in PROGRAMS/HELLO_S.EXE PROGRAMS/HELLO_C.EXE PROGRAMS/HELLO_M.EXE PROGRAMS/HELLO_L.EXE \
         PROGRAMS/ADD_S.EXE  PROGRAMS/ADD_C.EXE  PROGRAMS/ADD_M.EXE  PROGRAMS/ADD_L.EXE; do
  if [ ! -f "$f" ]; then
    echo "WARNING: expected file not found: $f"
  fi
done

