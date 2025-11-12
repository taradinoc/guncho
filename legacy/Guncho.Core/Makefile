# Makefile for Mono

SRC=Commands.cs ILogger.cs PriorityQueue.cs ReplacingStream.cs \
	Connection.cs Instance.cs Realm.cs Server.cs \
	Controller.cs Player.cs RealmFactory.cs ServerRunner.cs \
	Properties/AssemblyInfo.cs Properties/Settings.Designer.cs \
	XML/playerIndex.cs XML/realmIndex.cs XML/realmStorage.cs

CSC=csc
REFS=-r:../TextfyreVM.dll \
	-r:System.Runtime.Remoting.dll \
	-r:System.Web.dll

bin/Guncho.Core.dll: $(SRC)
	mkdir -p bin
	$(CSC) -out:bin/Guncho.Core.dll -target:library $(REFS) $(SRC)


