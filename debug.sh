#!/bin/sh
./clean.sh
./build.sh
gdb -tui ./out/program
