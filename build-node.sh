#!/bin/bash
export PATH="$HOME/.dotnet:$PATH"
cd /mnt/x/JammySeedFinder/src/MotelyJAML
dotnet publish Motely.Run/Motely.Run.csproj -c Release -p:NodeBuild=true
