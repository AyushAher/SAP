FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY SapApi/src/SapApi.Api/SapApi.Api.csproj SapApi/src/SapApi.Api/
COPY SapApi/src/SapApi.Infrastructure/SapApi.Infrastructure.csproj SapApi/src/SapApi.Infrastructure/
COPY SapApi/src/SapApi.Domain/SapApi.Domain.csproj SapApi/src/SapApi.Domain/
COPY SapApi/src/SapApi.Shared/SapApi.Shared.csproj SapApi/src/SapApi.Shared/
COPY JwtRotation/RotationJwt/RotatingJwt.csproj JwtRotation/RotationJwt/

RUN dotnet restore "SapApi/src/SapApi.Api/SapApi.Api.csproj" -c $BUILD_CONFIGURATION

COPY SapApi/ SapApi/
COPY JwtRotation/ JwtRotation/

WORKDIR /src/SapApi/src/SapApi.Api
RUN dotnet publish "SapApi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "SapApi.Api.dll"]
