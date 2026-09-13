FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

#RUN dotnet restore

#RUN dotnet publish "UrnWrapper.csproj" -c Release -o /src/publish

RUN groupadd -g 1000 1000 && useradd -g 1000 -s /bin/bash -m 1000 && echo "1000:123" | chpasswd
RUN usermod -aG sudo 1000

#USER 1000

ENTRYPOINT [ "bash", "/entrypoint.sh" ]


