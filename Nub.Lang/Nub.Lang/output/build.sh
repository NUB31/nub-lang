#!/bin/sh
nasm -g -felf64 out.asm -o out.o
nasm -g -felf64 ../input/core/string/strlen.asm -o strlen.o
nasm -g -felf64 ../input/core/string/strcmp.asm -o strcmp.o

ld -o out out.o strlen.o strcmp.o
