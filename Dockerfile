FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/ArticleService/ArticleService.csproj ./ArticleService/
RUN dotnet restore ./ArticleService/ArticleService.csproj

COPY src/ArticleService/ ./ArticleService/
RUN dotnet publish ./ArticleService/ArticleService.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "ArticleService.dll"]