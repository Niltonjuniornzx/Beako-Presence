#!/usr/bin/env bash
set -Eeuo pipefail

HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
if [[ ! -f "$HERE/beako.py" || ! -f "$HERE/testar.sh" ]]; then
  echo 'Erro: extraia todo o ZIP; beako.py e testar.sh precisam estar junto do instalador.' >&2
  exit 1
fi

PYTHON="$(command -v python3 || true)"
if [[ -z "$PYTHON" ]] || ! "$PYTHON" -c 'import tkinter' >/dev/null 2>&1; then
  echo 'Python3/Tkinter ausente; instalando dependências necessárias...'
  if command -v apt-get >/dev/null 2>&1; then
    sudo apt-get update
    sudo apt-get install -y python3 python3-tk
  elif command -v dnf >/dev/null 2>&1; then
    sudo dnf install -y python3 python3-tkinter
  elif command -v pacman >/dev/null 2>&1; then
    sudo pacman -S --needed python tk
  elif command -v zypper >/dev/null 2>&1; then
    sudo zypper install -y python3 python3-tk
  else
    echo 'Não foi possível instalar Tkinter automaticamente. Instale python3 e tkinter manualmente.' >&2
    exit 1
  fi
  PYTHON="$(command -v python3)"
fi
"$PYTHON" -c 'import tkinter' || { echo 'Falha: Tkinter não está disponível.' >&2; exit 1; }
"$PYTHON" -m py_compile "$HERE/beako.py"
bash -n "$HERE/testar.sh"

DIR="$HOME/.local/share/beako-presence"
BIN="$HOME/.local/bin"
APPS="$HOME/.local/share/applications"
mkdir -p "$DIR/assets" "$DIR/data" "$BIN" "$APPS"
install -m 644 "$HERE/beako.py" "$DIR/beako.py"
install -m 755 "$HERE/testar.sh" "$DIR/testar.sh"
if [[ -f "$HERE/assets/avatar.png" ]]; then
  install -m 644 "$HERE/assets/avatar.png" "$DIR/assets/avatar.png"
  install -m 644 "$HERE/assets/avatar-header.png" "$DIR/assets/avatar-header.png"
fi
if [[ -f "$HERE/data/confirmed.json" ]]; then
  install -m 644 "$HERE/data/confirmed.json" "$DIR/data/confirmed.json"
fi

install -m 644 "$HERE/data/catalog.json" "$DIR/data/catalog.json"

if [[ -d "$HERE/icons" ]]; then
  mkdir -p "$DIR/icons"
  for icon in "$HERE"/icons/*.png; do
    [[ -f "$icon" ]] && install -m 644 "$icon" "$DIR/icons/"
  done
fi

# Launcher no caminho do usuário. Escapar paths funciona até em HOME com espaços.
printf -v SRC '%q' "$DIR/beako.py"
printf -v PYCMD '%q' "$PYTHON"
printf '#!/usr/bin/env bash\nexec %s %s "$@"\n' "$PYCMD" "$SRC" > "$BIN/beako-presence"
chmod 755 "$BIN/beako-presence"
cat > "$APPS/beako-presence.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Beako Presence
Comment=Gerenciador de atividades do Discord
Exec=$BIN/beako-presence
Icon=$DIR/assets/avatar.png
Terminal=false
Categories=Game;Utility;
StartupNotify=true
EOF
chmod 644 "$APPS/beako-presence.desktop"
echo
printf 'Beako Presence instalado com sucesso!\nPara abrir: %s\n' "$BIN/beako-presence"
printf 'Ou procure Beako Presence no menu de aplicativos.\n'