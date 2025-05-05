#!/bin/sh
mkdir -p out

dotnet run --project lang/Nub.Lang example out/out.qbe

gcc -c -g -fno-stack-protector -fno-builtin std/baseline/gc.c -o out/gc.o
nasm -g -felf64 std/baseline/str_cmp.asm -o out/str_cmp.o
nasm -g -felf64 std/baseline/runtime.asm -o out/runtime.o

nasm -g -felf64 std/core/str_len.asm -o out/str_len.o
nasm -g -felf64 std/core/itoa.asm -o out/itoa.o

qbe out/out.qbe > out/out.s
gcc -c -g out/out.s -o out/out.o

gcc -no-pie -nostartfiles -o out/program out/gc.o out/str_cmp.o out/str_len.o out/itoa.o out/runtime.o out/out.o
