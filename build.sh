#!/bin/sh
mkdir -p out

echo "setup..."

dotnet publish -c Release src/compiler/Nub.Lang > /dev/null

if [[ $? -ne 0 ]] ; then
    printf "\x1b[31mYour compiler is not compiling!\x1b[0m\n"
    exit 1
fi

set -e

echo "compiling..."

nub example out/out.qbe

gcc -c -g -fno-stack-protector -fno-builtin src/runtime/gc.c -o out/gc.o
nasm -g -felf64 src/runtime/runtime.asm -o out/runtime.o

qbe out/out.qbe > out/out.s
gcc -c -g out/out.s -o out/out.o

gcc -nostartfiles -o out/program out/gc.o out/runtime.o out/out.o

echo "done..."
