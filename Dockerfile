# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer as long as the csproj doesn't change)
COPY DCBot.csproj ./
RUN dotnet restore DCBot.csproj

# Build + publish
COPY . .
RUN dotnet publish DCBot.csproj -c Release -o /out /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS bot
WORKDIR /app

COPY --from=build /out .

ENTRYPOINT ["dotnet", "DCBot.dll"]
