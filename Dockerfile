# ================================
# Stage 1: Build
# ================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj trước để cache layer restore
COPY ["MovieWeb.csproj", "."]
RUN dotnet restore "./MovieWeb.csproj"

# Copy toàn bộ source code
COPY . .

# Build và publish
RUN dotnet publish "./MovieWeb.csproj" -c Release -o /app/publish --no-restore

# ================================
# Stage 2: Runtime
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Cài tzdata để hỗ trợ timezone trên Linux
RUN apt-get update && apt-get install -y tzdata curl && rm -rf /var/lib/apt/lists/*
ENV TZ=Asia/Bangkok

# Copy published app từ stage build
COPY --from=build /app/publish .

# Port app lắng nghe (nội bộ, Nginx sẽ proxy vào)
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MovieWeb.dll"]
