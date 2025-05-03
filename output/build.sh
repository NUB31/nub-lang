#!/bin/sh
gcc -c -g -O2 -fno-stack-protector -fno-builtin ../input/baseline/gc.c -o gc.o
nasm -g -felf64 ../input/baseline/str_cmp.asm -o str_cmp.o

nasm -g -felf64 ../input/core/str_len.asm -o str_len.o
nasm -g -felf64 ../input/core/arr_size.asm -o arr_size.o
nasm -g -felf64 ../input/core/itoa.asm -o itoa.o

nasm -g -felf64 out.asm -o out.o

gcc -no-pie -nostartfiles -o out gc.o str_cmp.o str_len.o arr_size.o itoa.o out.o
