#!/bin/bash
set -e

dotnet restore

dotnet publish "UrnWrapper.csproj" -c Release -o /urnoutput/bin

chmod -R 755 /urnoutput/bin

chown -R "1000:1000" /urnoutput/bin
