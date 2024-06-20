FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

WORKDIR /app/publish
ENTRYPOINT ["dotnet", "Zadanie2.dll"]
