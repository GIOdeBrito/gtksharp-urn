#!/bin/bash

dotnet restore

dotnet publish "UrnWrapper.csproj" -c Release -o /src/publish

chmod -R 777 /src/publish
