#!/bin/sh
nasm -g -felf64 out.asm -o out.o
nasm -g -felf64 ../input/core/string.asm -o string.o

ld -o out out.o string.o
