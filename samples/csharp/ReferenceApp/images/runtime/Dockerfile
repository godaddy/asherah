# An example Dockerfile for running the sample. Will capture any interesting findings for containerized flows.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1

# NOTE : dotnet enables debugging and profiling by default causing filesystem writes
# Disabling them ensures that our application can run in a read-only container.
ENV COMPlus_EnableDiagnostics=0

RUN apt-get update && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app/publish
ADD ReferenceApp/bin/Release/netcoreapp3.1/publish/ .

ENTRYPOINT ["dotnet", "ReferenceApp.dll"]
