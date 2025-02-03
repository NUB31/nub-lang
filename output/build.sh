#!/bin/sh

# baseline
nasm -g -felf64 ../input/baseline/gc.asm -o gc.o
nasm -g -felf64 ../input/baseline/alloc.asm -o alloc.o
nasm -g -felf64 ../input/baseline/str_cmp.asm -o str_cmp.o

# core
nasm -g -felf64 ../input/core/str_len.asm -o str_len.o
nasm -g -felf64 ../input/core/arr_size.asm -o arr_size.o
nasm -g -felf64 ../input/core/itoa.asm -o itoa.o

# program
nasm -g -felf64 out.asm -o out.o

ld -o out str_len.o arr_size.o itoa.o alloc.o gc.o str_cmp.o out.o
