#!/bin/sh
mkdir -p out

dotnet run --project lang/Nub.Lang example out/out.qbe

gcc -c -g -fno-stack-protector -fno-builtin std/gc.c -o out/gc.o
nasm -g -felf64 std/runtime.asm -o out/runtime.o

qbe out/out.qbe > out/out.s
gcc -c -g out/out.s -o out/out.o

gcc -no-pie -nostartfiles -o out/program out/gc.o out/runtime.o out/out.o
