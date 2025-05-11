#!/bin/sh
set -e
./clean.sh
./build.sh
./out/program
echo "Process exited with status code $?"
