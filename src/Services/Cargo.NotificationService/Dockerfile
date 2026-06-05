FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/Cargo.NotificationService/Cargo.NotificationService.csproj", "src/Services/Cargo.NotificationService/"]
COPY ["src/BuildingBlocks/Cargo.BuildingBlocks/Cargo.BuildingBlocks.csproj", "src/BuildingBlocks/Cargo.BuildingBlocks/"]
COPY ["src/BuildingBlocks/Cargo.Observability/Cargo.Observability.csproj", "src/BuildingBlocks/Cargo.Observability/"]
RUN dotnet restore "./src/Services/Cargo.NotificationService/Cargo.NotificationService.csproj"
COPY . .
WORKDIR "/src/src/Services/Cargo.NotificationService"
RUN dotnet build "./Cargo.NotificationService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Cargo.NotificationService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cargo.NotificationService.dll"]
