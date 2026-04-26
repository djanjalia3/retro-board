# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY RetroBoard.sln ./
COPY src/RetroBoard.Domain/*.csproj src/RetroBoard.Domain/
COPY src/RetroBoard.Application/*.csproj src/RetroBoard.Application/
COPY src/RetroBoard.Infrastructure/*.csproj src/RetroBoard.Infrastructure/
COPY src/RetroBoard.Api/*.csproj src/RetroBoard.Api/
RUN dotnet restore src/RetroBoard.Api/RetroBoard.Api.csproj

COPY src/ src/
RUN dotnet publish src/RetroBoard.Api/RetroBoard.Api.csproj \
    -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RetroBoard.Api.dll"]
