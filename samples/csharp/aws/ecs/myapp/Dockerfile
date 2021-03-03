FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS builder

WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out



FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

# NOTE : dotnet enables debugging and profiling by default causing filesystem writes
# Disabling them ensures that our application can run in a read-only container.
ENV COMPlus_EnableDiagnostics=0

WORKDIR /app
COPY --from=builder /app/out .
ADD entrypoint.sh .

ENTRYPOINT ["./entrypoint.sh"]

ENV ASPNETCORE_URLS http://+:8000
EXPOSE 8000
