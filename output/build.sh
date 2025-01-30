#!/bin/sh
nasm -g -felf64 out.asm -o out.o
nasm -g -felf64 ../input/core/strlen.asm -o strlen.o
nasm -g -felf64 ../input/core/arrsize.asm -o arrsize.o

ld -o out strlen.o arrsize.o out.o
