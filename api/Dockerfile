FROM node:22-bookworm-slim AS frontend
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api/ ./
RUN dotnet publish src/AssetKeeper.Api/AssetKeeper.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
RUN rm -rf /app/publish/wwwroot
COPY --from=frontend /web/dist/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "AssetKeeper.Api.dll"]
