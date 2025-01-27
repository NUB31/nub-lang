#!/bin/bash

nasm -g -felf64 out.asm -o out.o
ld out.o -o out

gdb -tui out

rm out.o
rm out