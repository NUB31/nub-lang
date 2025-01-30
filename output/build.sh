#!/bin/sh
nasm -g -felf64 out.asm -o out.o
nasm -g -felf64 ../input/core/strlen.asm -o strlen.o

ld -o out strlen.o out.o
