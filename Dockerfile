FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FraudEngine.Api/FraudEngine.Api.csproj FraudEngine.Api/
RUN dotnet restore FraudEngine.Api/FraudEngine.Api.csproj

COPY FraudEngine.Api/ FraudEngine.Api/
RUN dotnet publish FraudEngine.Api/FraudEngine.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/false fraudengine \
    && mkdir -p /app/data \
    && chown -R fraudengine:fraudengine /app/data
USER fraudengine

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__FraudDb="Data Source=/app/data/fraudengine.db"

EXPOSE 8080

ENTRYPOINT ["dotnet", "FraudEngine.Api.dll"]
