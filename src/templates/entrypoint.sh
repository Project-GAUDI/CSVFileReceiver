#! /bin/sh

cat application.info

exec dotnet CSVFileReceiver.dll "$@"
