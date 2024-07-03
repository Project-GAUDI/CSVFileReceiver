FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY ./ ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:6.0-bullseye-slim
WORKDIR /app
COPY --from=build-env /app/out ./

RUN useradd -ms /bin/bash moduleuser
USER moduleuser

LABEL org.opencontainers.image.description="CSVFileReceiver is an Azure IoT edge module that monitors CSV files and sends records of detected CSV files."

ENTRYPOINT ["dotnet", "CSVFileReceiver.dll"]