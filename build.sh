#!/bin/zsh
# Compile the dc input tool. Requires Xcode Command Line Tools (xcode-select --install).
cd "${0:a:h}" && swiftc -O dc.swift -o dc && echo "built ./dc"
