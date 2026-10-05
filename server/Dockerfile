# Build context: server/
#
# Secrets are NEVER baked into the image — inject
# TIDAL_CLIENT_ID / TIDAL_CLIENT_SECRET through your hosting
# provider's environment/secret configuration at runtime.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TidalDiscord.Api/TidalDiscord.Api.csproj TidalDiscord.Api/
RUN dotnet restore TidalDiscord.Api/TidalDiscord.Api.csproj

COPY TidalDiscord.Api/ TidalDiscord.Api/
RUN dotnet publish TidalDiscord.Api/TidalDiscord.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "TidalDiscord.Api.dll"]
