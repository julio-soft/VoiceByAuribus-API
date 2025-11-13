FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5037

ENV ASPNETCORE_URLS=http://+:5037

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["VoiceByAuribus.API/VoiceByAuribus-API.csproj", "VoiceByAuribus.API/"]
RUN dotnet restore "VoiceByAuribus.API/VoiceByAuribus-API.csproj"
COPY . .
WORKDIR "/src/VoiceByAuribus.API"
RUN dotnet build "VoiceByAuribus-API.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "VoiceByAuribus-API.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VoiceByAuribus-API.dll"]
