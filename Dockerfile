# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so restore can be cached
COPY CodePulse.API/CodePulse.API.csproj CodePulse.API/
RUN dotnet restore CodePulse.API/CodePulse.API.csproj

# Copy everything else and build
COPY . .
WORKDIR /src/CodePulse.API
RUN dotnet publish CodePulse.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CodePulse.API.dll"]
