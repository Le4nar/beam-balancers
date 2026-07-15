FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy everything
COPY . .

# Pre-build to cache packages
RUN dotnet restore Core.csproj
RUN dotnet publish Core.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/out .
# Copy modules for runtime compilation
COPY --from=build /app/module ./module
# Copy example config
COPY --from=build /app/init.conf.example ./init.conf.example

EXPOSE 9118

VOLUME ["/app/init.conf", "/app/database"]

ENTRYPOINT ["dotnet", "Core.dll"]
