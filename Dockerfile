# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SanitizeService.sln ./
COPY src/SanitizeService.Api/SanitizeService.Api.csproj src/SanitizeService.Api/
COPY src/SanitizeService.Application/SanitizeService.Application.csproj src/SanitizeService.Application/
COPY src/SanitizeService.Domain/SanitizeService.Domain.csproj src/SanitizeService.Domain/

RUN dotnet restore src/SanitizeService.Api/SanitizeService.Api.csproj

COPY src/ src/

RUN dotnet publish src/SanitizeService.Api/SanitizeService.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SanitizeService.Api.dll"]
