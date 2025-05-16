#!/bin/sh
set -e
./clean.sh
./build.sh
gdb -tui ./out/program
