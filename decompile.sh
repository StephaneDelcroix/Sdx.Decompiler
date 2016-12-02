#!/bin/bash
Configuration="Debug"
mono ./Decompiler/bin/$Configuration/decompile.exe $@
