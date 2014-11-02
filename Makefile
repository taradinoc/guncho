# Makefile for Mono

BINS=Guncho.Core/bin/Guncho.Core.dll \
	GunchoConsole/bin/GunchoConsole.exe \
	TextfyreVM.dll

all: force_build
	cd Guncho.Core; make
	cd GunchoConsole; make
	mkdir -p bin
	cp $(BINS) bin

force_build:
	true
