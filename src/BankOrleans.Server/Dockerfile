FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

EXPOSE 11111
EXPOSE 30000

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["src/BankOrleans.Interfaces/BankOrleans.Interfaces.csproj", "src/BankOrleans.Interfaces/"]
COPY ["src/BankOrleans.Grains/BankOrleans.Grains.csproj", "src/BankOrleans.Grains/"]
COPY ["src/BankOrleans.Server/BankOrleans.Server.csproj", "src/BankOrleans.Server/"]
RUN dotnet restore "src/BankOrleans.Server/BankOrleans.Server.csproj"
COPY . .
WORKDIR "/src/src/BankOrleans.Server"
RUN dotnet build "BankOrleans.Server.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "BankOrleans.Server.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER root
RUN apt-get update && \
    apt-get install wget postgresql-client --yes && \
    mkdir --parents ~/.postgresql && \
    wget "https://storage.yandexcloud.net/cloud-certs/CA.pem" \
         --output-document ~/.postgresql/root.crt && \
    chmod 0600 ~/.postgresql/root.crt

ENTRYPOINT ["dotnet", "BankOrleans.Server.dll"]
