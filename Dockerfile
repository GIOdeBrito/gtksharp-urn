FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

#RUN dotnet restore

CMD ["sleep", "infinity"]
