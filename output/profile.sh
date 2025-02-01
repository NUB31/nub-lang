#!/bin/sh
./build.sh
valgrind -s ./out
