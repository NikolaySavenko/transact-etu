FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["src/BankOrleans.Interfaces/BankOrleans.Interfaces.csproj", "src/BankOrleans.Interfaces/"]
COPY ["src/BankOrleans.Grains/BankOrleans.Grains.csproj", "src/BankOrleans.Grains/"]
COPY ["src/BankOrleans.Client/BankOrleans.Client.csproj", "src/BankOrleans.Client/"]
RUN dotnet restore "src/BankOrleans.Client/BankOrleans.Client.csproj"
COPY . .
WORKDIR "/src/src/BankOrleans.Client"
RUN dotnet build "BankOrleans.Client.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "BankOrleans.Client.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BankOrleans.Client.dll"]
