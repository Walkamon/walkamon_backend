FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY Walkamon/Walkamon.csproj Walkamon/
COPY BLL/BLL.csproj BLL/
COPY DAL/DAL.csproj DAL/
RUN dotnet restore Walkamon/Walkamon.csproj

COPY Walkamon/ Walkamon/
COPY BLL/ BLL/
COPY DAL/ DAL/
RUN dotnet publish Walkamon/Walkamon.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends ca-certificates curl tzdata \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    TZ=Asia/Ho_Chi_Minh
EXPOSE 8080

USER 1654:1654
ENTRYPOINT ["dotnet", "Walkamon.dll"]
