# 빌드 단계 — Maven으로 jar 만드는 단계에 해당
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/BathErp/BathErp.csproj src/BathErp/
RUN dotnet restore src/BathErp/BathErp.csproj
COPY src/ src/
RUN dotnet publish src/BathErp/BathErp.csproj -c Release -o /app

# 실행 단계 — 런타임만 담은 가벼운 이미지
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BathErp.dll"]
