FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/MyAssistant.Domain/MyAssistant.Domain.csproj backend/MyAssistant.Domain/
COPY backend/MyAssistant.Application/MyAssistant.Application.csproj backend/MyAssistant.Application/
COPY backend/MyAssistant.Infrastructure/MyAssistant.Infrastructure.csproj backend/MyAssistant.Infrastructure/
COPY backend/MyAssistant.API/MyAssistant.API.csproj backend/MyAssistant.API/
RUN dotnet restore backend/MyAssistant.API/MyAssistant.API.csproj

COPY backend/ backend/
WORKDIR /src/backend/MyAssistant.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /app/data
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyAssistant.API.dll"]
