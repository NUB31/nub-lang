#!/bin/bash

nasm -g -felf64 out.asm -o out.o
ld out.o -o out
./out

rm out.o

echo "Process exited with status code $?"