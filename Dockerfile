# ---------- Stage 1: Build the Flutter web frontend ----------
FROM ghcr.io/cirruslabs/flutter:stable AS flutter
WORKDIR /src/mobile
COPY mobile/pubspec.yaml mobile/pubspec.lock ./
RUN dart pub get
COPY mobile/ .
# API_BASE_URL is left empty so the web app calls the API on the same origin.
RUN flutter build web --release --dart-define=API_BASE_URL=

# ---------- Stage 2: Build the .NET 8 API ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet
WORKDIR /src
COPY backend/MyAssistant.API/MyAssistant.API.csproj backend/MyAssistant.API/
COPY backend/MyAssistant.Application/MyAssistant.Application.csproj backend/MyAssistant.Application/
COPY backend/MyAssistant.Domain/MyAssistant.Domain.csproj backend/MyAssistant.Domain/
COPY backend/MyAssistant.Infrastructure/MyAssistant.Infrastructure.csproj backend/MyAssistant.Infrastructure/
COPY backend/MyAssistant.Tests/MyAssistant.Tests.csproj backend/MyAssistant.Tests/
RUN dotnet restore backend/MyAssistant.API/MyAssistant.API.csproj
COPY backend/MyAssistant.API backend/MyAssistant.API
COPY backend/MyAssistant.Application backend/MyAssistant.Application
COPY backend/MyAssistant.Domain backend/MyAssistant.Domain
COPY backend/MyAssistant.Infrastructure backend/MyAssistant.Infrastructure
COPY backend/MyAssistant.Tests backend/MyAssistant.Tests
RUN dotnet publish backend/MyAssistant.API/MyAssistant.API.csproj -c Release -o /out --no-restore

# ---------- Stage 3: Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5088
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5088
COPY --from=dotnet /out .
COPY --from=flutter /src/mobile/build/web ./wwwroot
ENTRYPOINT ["dotnet", "MyAssistant.API.dll"]
