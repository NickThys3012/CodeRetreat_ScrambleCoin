# Multi-stage build for ScrambleCoin.Web (Blazor Server).
# Build context MUST be the repository root (it needs sibling projects).
#   docker build -f src/ScrambleCoin.Web/Dockerfile -t scramblecoin-web .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (better layer caching): copy the solution + all csproj files.
COPY ScrambleCoin.sln ./
COPY global.json ./
COPY src/ScrambleCoin.Domain/ScrambleCoin.Domain.csproj src/ScrambleCoin.Domain/
COPY src/ScrambleCoin.Application/ScrambleCoin.Application.csproj src/ScrambleCoin.Application/
COPY src/ScrambleCoin.Infrastructure/ScrambleCoin.Infrastructure.csproj src/ScrambleCoin.Infrastructure/
COPY src/ScrambleCoin.Web/ScrambleCoin.Web.csproj src/ScrambleCoin.Web/
RUN dotnet restore src/ScrambleCoin.Web/ScrambleCoin.Web.csproj

# Copy the rest of the source and publish.
COPY . .
RUN dotnet publish src/ScrambleCoin.Web/ScrambleCoin.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ScrambleCoin.Web.dll"]
