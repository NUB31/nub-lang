#!/bin/sh
nasm -g -felf64 out.asm -o out.o
nasm -g -felf64 core/strlen.asm -o strlen.o
nasm -g -felf64 core/strcmp.asm -o strcmp.o

ld -o out out.o strlen.o strcmp.o
