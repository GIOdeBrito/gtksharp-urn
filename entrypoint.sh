#!/bin/bash
set -e

dotnet restore

dotnet publish "UrnWrapper.csproj" -c Release -o /urnoutput/bin

chmod -R 755 /src/obj
chmod -R 755 /src/publish

chown "1000:1000" /src/publish
