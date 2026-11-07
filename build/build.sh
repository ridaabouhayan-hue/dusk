#!/bin/sh
# Snel controleren op Linux met Mono (alleen voor ontwikkelen; de echte build is build.ps1 op Windows).
# Controleert ook dat alles in C# 5 blijft, net als de compiler in Windows.
set -e
cd "$(dirname "$0")/.."
mkdir -p out
REFS="-r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll"
mcs -langversion:5 -warnaserror+ -target:winexe -out:out/Dusk.exe $REFS src/*.cs
mcs -langversion:5 -target:exe -main:Dusk.Tests -out:out/Dusk.Tests.exe $REFS src/*.cs tests/*.cs
xvfb-run -a mono out/Dusk.Tests.exe
xvfb-run -a mono out/Dusk.exe --zelftest --map out/schermen
