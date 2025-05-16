#!/bin/sh
set -e

mkdir -p out

echo "setup..."

dotnet publish -c Release src/compiler/Nub.Lang

echo "compiling..."

nub example out/out.qbe

nasm -g -felf64 src/runtime/runtime.asm -o out/runtime.o

qbe out/out.qbe > out/out.s
gcc -c -g out/out.s -o out/out.o

gcc -nostartfiles -o out/program out/runtime.o out/out.o

echo "done..."
