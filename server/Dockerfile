# Rogue Pirates playtest API. Build context: the `server` folder (Railway root directory).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RoguePirates.Api/RoguePirates.Api.csproj RoguePirates.Api/
RUN dotnet restore RoguePirates.Api/RoguePirates.Api.csproj
COPY RoguePirates.Api/ RoguePirates.Api/
RUN dotnet publish RoguePirates.Api/RoguePirates.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Railway injects PORT; 8080 is the fallback. DATABASE_URL comes from the Postgres plugin.
# Without DATABASE_URL the API falls back to SQLite in /tmp (lost on redeploy; for a quick check only).
ENV PORT=8080 \
    SQLITE_PATH=/tmp/roguepirates.db
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "RoguePirates.Api.dll"]
