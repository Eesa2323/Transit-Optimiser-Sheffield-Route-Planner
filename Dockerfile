FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TransitOptimizer.Api/TransitOptimizer.Api.csproj TransitOptimizer.Api/
RUN dotnet restore TransitOptimizer.Api/TransitOptimizer.Api.csproj
COPY TransitOptimizer.Api/ TransitOptimizer.Api/
RUN dotnet publish TransitOptimizer.Api/TransitOptimizer.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TransitOptimizer.Api.dll"]
