#!/bin/sh
nasm -g -felf64 out.asm -o out.o
nasm -g -felf64 ../input/core/str_len.asm -o str_len.o
nasm -g -felf64 ../input/core/arr_size.asm -o arr_size.o
nasm -g -felf64 ../input/core/itoa.asm -o itoa.o

ld -o out str_len.o arr_size.o itoa.o out.o
