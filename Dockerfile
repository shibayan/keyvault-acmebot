# Base image for Azure Functions .NET 6
FROM mcr.microsoft.com/azure-functions/dotnet:4-dotnet8 AS base
WORKDIR /home/site/wwwroot
EXPOSE 80

# Stage for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["KeyVault.Acmebot.sln", "."]
COPY ["KeyVault.Acmebot/KeyVault.Acmebot.csproj", "KeyVault.Acmebot/"]
COPY ["ACMESharpCore/src/ACMESharp/ACMESharp.csproj", "ACMESharpCore/src/ACMESharp/"]

# Restore dependencies
RUN dotnet restore "KeyVault.Acmebot.sln"

# Copy the rest of the application code
COPY . .

# Build and publish the application
WORKDIR "/src/KeyVault.Acmebot"
RUN dotnet build "KeyVault.Acmebot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KeyVault.Acmebot.csproj"     -c Release     -o /app/publish     --no-restore     /p:UseAppHost=false

# Final stage: copy published application to the base image
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .

# Set environment variables for Azure Functions worker
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true
