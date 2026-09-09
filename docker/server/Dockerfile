FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src ./src
RUN dotnet publish src/Wilson.Server/Wilson.Server.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
EXPOSE 9400
ENTRYPOINT ["dotnet", "Wilson.Server.dll", "wilson.json"]
