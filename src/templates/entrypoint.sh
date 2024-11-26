#! /bin/bash

cat application.info

exec dotnet CSVFileReceiver.dll "$@"
