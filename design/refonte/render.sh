#!/bin/zsh
# Rend une maquette HTML en PNG (×2). Usage : ./render.sh 01_partie_A <nb de téléphones>
cd ${0:A:h}
n=${2:-3}
w=$(( n * 390 + (n - 1) * 36 + 80 ))
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars \
  --force-device-scale-factor=2 --window-size=$w,990 --virtual-time-budget=4000 \
  --screenshot=$1.png "file://$PWD/$1.html" 2>/dev/null
echo "$1.png"
