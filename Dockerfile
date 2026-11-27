# Multi-stage Docker build for GarmentERP Web API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore
COPY src/GarmentERP.Shared/GarmentERP.Shared.csproj src/GarmentERP.Shared/
COPY src/GarmentERP.Domain/GarmentERP.Domain.csproj src/GarmentERP.Domain/
COPY src/GarmentERP.Application/GarmentERP.Application.csproj src/GarmentERP.Application/
COPY src/GarmentERP.Reporting/GarmentERP.Reporting.csproj src/GarmentERP.Reporting/
COPY src/GarmentERP.Infrastructure/GarmentERP.Infrastructure.csproj src/GarmentERP.Infrastructure/
COPY src/GarmentERP.SyncService/GarmentERP.SyncService.csproj src/GarmentERP.SyncService/
COPY src/GarmentERP.Api/GarmentERP.Api.csproj src/GarmentERP.Api/

RUN dotnet restore src/GarmentERP.Api/GarmentERP.Api.csproj

# Copy full source and build
COPY src/ src/
WORKDIR /src/src/GarmentERP.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "GarmentERP.Api.dll"]
