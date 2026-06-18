# ===== Étape 1 : compilation =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GestionBarRestaurant.sln ./
COPY Domaine/Domaine.csproj Domaine/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Presentation/Presentation.csproj Presentation/
RUN dotnet restore Presentation/Presentation.csproj

COPY . .
RUN dotnet publish Presentation/Presentation.csproj -c Release -o /app/publish /p:UseAppHost=false

# ===== Étape 2 : image d'exécution =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Répertoire de données persistant (monté en volume)
ENV DATA_DIR=/app/Data
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
RUN mkdir -p /app/Data

EXPOSE 8080
ENTRYPOINT ["dotnet", "Presentation.dll"]
