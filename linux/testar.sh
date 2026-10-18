#!/usr/bin/env bash
set -Eeuo pipefail

MODE='interactive'
if [[ "${1:-}" == '--managed' ]]; then
  MODE='managed'
  shift
fi
NAME="${1:-Risk of Rain 2.exe}"
# Use the same validator as the interface, including Windows separators.
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
NAME="$(python3 - "$HERE" "$NAME" <<'PY'
import sys
sys.path.insert(0, sys.argv[1])
from beako import catalog_filename
name = catalog_filename(sys.argv[2])
if name is None:
    print("Caminho de executável inválido.", file=sys.stderr)
    raise SystemExit(2)
print(name)
PY
)"
SLEEP_BIN="$(command -v sleep)"
if [[ ! -x "$SLEEP_BIN" ]]; then
  echo 'Programa sleep não encontrado.' >&2
  exit 3
fi
DIR="$(mktemp -d -t beako-process-XXXXXX)"
PID=''
cleanup() {
  trap - EXIT HUP INT TERM
  if [[ -n "$PID" ]]; then
    kill "$PID" 2>/dev/null || true
    wait "$PID" 2>/dev/null || true
  fi
  rm -rf -- "$DIR"
}
trap cleanup EXIT HUP INT TERM
mkdir -p -- "$(dirname -- "$DIR/$NAME")"
cp -- "$SLEEP_BIN" "$DIR/$NAME"
chmod +x -- "$DIR/$NAME"
"$DIR/$NAME" 3600 &
PID=$!
if [[ "$MODE" == 'managed' ]]; then
  wait "$PID" || true
else
  printf '\nProcesso de teste iniciado (PID %s).\n' "$PID"
  printf 'Executável: %s\n' "$DIR/$NAME"
  printf 'Verifique Discord > Configurações > Jogos registrados.\n'
  printf 'Se não aparecer, tente Adicionar jogo e confira a lista.\n'
  printf 'Pressione ENTER para encerrar o teste.\n'
  read -r _ || true
fi