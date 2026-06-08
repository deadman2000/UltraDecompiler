#!/usr/bin/env bash
#
# build_ci.sh — сборка эталонных .EXE QuickC через DOSBox (для Linux / GitHub Actions).
#
# Используется в CI, чтобы тесты (LibMatchingTests и др.) получали свежие
# скомпилированные примеры ПЕРЕД запуском dotnet test.
#
# Требования: dosbox-staging (предпочтительно) или обычный dosbox в PATH.
# В GitHub Actions мы пытаемся поставить dosbox-staging из PPA, с фоллбэком на dosbox.
#
# Скрипт монтирует каталог QuickC как диск C: и запускает build_dos.bat внутри эмулятора.
# Полученные .EXE появляются в реальной файловой системе в QuickC/PROGRAMS/.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== Building QuickC reference examples via DOSBox ==="
echo "Working dir: $(pwd)"

# Выбираем команду dosbox (предпочитаем dosbox-staging, если есть)
DOSBOX=""
if command -v dosbox-staging >/dev/null 2>&1; then
  DOSBOX="dosbox-staging"
elif command -v dosbox >/dev/null 2>&1; then
  DOSBOX="dosbox"
else
  echo "ERROR: neither dosbox-staging nor dosbox found in PATH."
  echo ""
  echo "На Ubuntu runner установи один из пакетов:"
  echo "  sudo apt-get install -y dosbox"
  echo "  или (лучше):"
  echo "  sudo add-apt-repository -y ppa:dosbox-staging/stable"
  echo "  sudo apt-get update && sudo apt-get install -y dosbox-staging"
  exit 1
fi

echo "Using: $DOSBOX"
echo "Config: dosbox-ci.conf"
echo "Current working directory (host): $(pwd)"

ls -l dosbox-ci.conf build_dos.bat 2>/dev/null || echo "WARNING: conf or bat not found in current dir"

# Важно для headless/CI окружения (GitHub Actions и т.п.)
# Без dummy-драйверов DOSBox часто не может инициализировать видео и выходит до autoexec.
export SDL_VIDEODRIVER=dummy
export SDL_AUDIODRIVER=dummy

echo "Launching DOSBox in headless mode (dummy video/audio drivers)..."

# Используем конфиг с [autoexec] — значительно надёжнее длинной цепочки -c,
# особенно когда падает бэк на классический dosbox из Ubuntu-репозиториев.
# В конфиге: mount c ., настройка LIB/INCLUDE, cd PROGRAMS, вызов build_dos.bat

"$DOSBOX" -conf dosbox-ci.conf -exit 2>&1 | cat

echo "=== DOSBox QuickC build completed ==="

echo
echo "Produced executables:"
ls -l PROGRAMS/*_?.EXE 2>/dev/null || echo "(no *_?.EXE files found — build may have failed)"
echo
