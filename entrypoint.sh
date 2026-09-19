#!/bin/bash
set -e

dotnet restore

dotnet publish "UrnWrapper.csproj" -c Release -o /src/publish

chmod -R 755 /src/publish
