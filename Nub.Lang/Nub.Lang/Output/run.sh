#!/bin/bash

nasm -g -felf64 out.asm -o out.o
ld out.o -o out

./out
echo "Process exited with status code $?"

rm out.o
rm out