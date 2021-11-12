#!/bin/bash
rm Madhunt.zip Madhunt.dll
dotnet build Code/Madhunt/Madhunt.csproj
zip Madhunt.zip -r LICENSE.txt everest.yaml Ahorn Dialog Graphics Maps Madhunt.dll debug.bin