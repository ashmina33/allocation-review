# Optional: container image for hosting (e.g. Render). Not needed for local development.
# Build:  docker build -t allocation-review .
# Render builds this file on its own servers, so Docker is not required on your machine.

# ---- 1. Angular production build (written into the API's wwwroot) ----
FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json frontend/.npmrc ./
RUN npm ci
COPY frontend/ ./
RUN npx ng build --configuration production

# ---- 2. .NET publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
COPY backend/ ./
COPY --from=web /src/backend/src/AllocationReview.Api/wwwroot ./src/AllocationReview.Api/wwwroot
RUN dotnet publish src/AllocationReview.Api/AllocationReview.Api.csproj -c Release -o /app/publish

# ---- 3. Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AllocationReview.Api.dll"]
