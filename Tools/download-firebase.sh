#!/bin/sh
# Baixa os pacotes do Firebase para Unity usados pelo projeto (não ficam no git por serem grandes).
# Uso: sh Tools/download-firebase.sh   (rodar na raiz do projeto, com a Unity fechada)
VERSION=13.17.0
mkdir -p Packages/firebase
for p in app analytics crashlytics; do
  f="Packages/firebase/com.google.firebase.$p-$VERSION.tgz"
  [ -f "$f" ] && continue
  curl -fL -o "$f" "https://dl.google.com/games/registry/unity/com.google.firebase.$p/com.google.firebase.$p-$VERSION.tgz" || exit 1
done
echo "Firebase $VERSION pronto em Packages/firebase/"
