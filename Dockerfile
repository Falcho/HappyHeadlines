FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE_NAME
WORKDIR /source

COPY src/${SERVICE_NAME}/${SERVICE_NAME}.csproj ./${SERVICE_NAME}/
RUN dotnet restore ./${SERVICE_NAME}/${SERVICE_NAME}.csproj

COPY src/${SERVICE_NAME}/ ./${SERVICE_NAME}/
RUN dotnet publish ./${SERVICE_NAME}/${SERVICE_NAME}.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SERVICE_NAME
ENV SERVICE_DLL=${SERVICE_NAME}.dll
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8000
EXPOSE 8000

ENTRYPOINT ["sh", "-c", "exec dotnet $SERVICE_DLL"]