# Makefile for Mono

SRC=Program.cs Properties/AssemblyInfo.cs

CSC=csc
REFS=-r:../Guncho.Core/bin/Guncho.Core.dll

bin/GunchoConsole.exe: $(SRC) ../Guncho.Core/bin/Guncho.Core.dll
	mkdir -p bin
	$(CSC) -out:bin/GunchoConsole.exe -target:exe $(REFS) $(SRC)
