FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY SapApi/src/SapApi.Api/SapApi.Api.csproj SapApi/src/SapApi.Api/
COPY SapApi/src/SapApi.Infrastructure/SapApi.Infrastructure.csproj SapApi/src/SapApi.Infrastructure/
COPY SapApi/src/SapApi.Domain/SapApi.Domain.csproj SapApi/src/SapApi.Domain/
COPY SapApi/src/SapApi.Shared/SapApi.Shared.csproj SapApi/src/SapApi.Shared/
COPY JwtRotation/RotationJwt/RotatingJwt.csproj JwtRotation/RotationJwt/

RUN dotnet restore "SapApi/src/SapApi.Api/SapApi.Api.csproj"

COPY SapApi/ SapApi/
COPY JwtRotation/ JwtRotation/

WORKDIR /src/SapApi/src/SapApi.Api
RUN dotnet publish "SapApi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Keys are prepared in CI before build; copy from context so publish cache cannot serve stale PEMs.
COPY SapApi/src/SapApi.Api/Keys/*.pem ./Keys/
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "SapApi.Api.dll"]
