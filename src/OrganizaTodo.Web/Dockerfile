FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/OrganizaTodo.Web/OrganizaTodo.Web.csproj", "src/OrganizaTodo.Web/"]
RUN dotnet restore "src/OrganizaTodo.Web/OrganizaTodo.Web.csproj"
COPY . .
RUN dotnet publish "src/OrganizaTodo.Web/OrganizaTodo.Web.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrganizaTodo.Web.dll"]
