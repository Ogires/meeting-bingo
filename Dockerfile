# Stage 1: build the Angular app
FROM node:24-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY data/ /src/data/
COPY web/ ./
RUN npm run build

# Stage 2: restore, build and publish the API, with the Angular files in wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Only what restore needs, so the restore layer stays cached until a package changes
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/MeetingBingo.Api/MeetingBingo.Api.csproj src/MeetingBingo.Api/packages.lock.json src/MeetingBingo.Api/
COPY src/MeetingBingo.Core/MeetingBingo.Core.csproj src/MeetingBingo.Core/packages.lock.json src/MeetingBingo.Core/
COPY src/MeetingBingo.Infrastructure/MeetingBingo.Infrastructure.csproj src/MeetingBingo.Infrastructure/packages.lock.json src/MeetingBingo.Infrastructure/
COPY src/MeetingBingo.ServiceDefaults/MeetingBingo.ServiceDefaults.csproj src/MeetingBingo.ServiceDefaults/packages.lock.json src/MeetingBingo.ServiceDefaults/
RUN dotnet restore src/MeetingBingo.Api/MeetingBingo.Api.csproj --locked-mode
# The rest of the code: same analyzer rules as on a dev machine, the phrases, the projects and the built web
COPY .editorconfig ./
COPY data/ data/
COPY src/ src/
COPY --from=web /src/web/dist/web/browser/ src/MeetingBingo.Api/wwwroot/
RUN dotnet publish src/MeetingBingo.Api/MeetingBingo.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Stage 3: only the runtime (no SDK, no shell, no package manager) and a non-root user
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "MeetingBingo.Api.dll"]
