# Stage 1: Build (Nâng lên 10.0)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy toàn bộ mã nguồn
COPY . .

# Tìm và restore giải pháp .sln
RUN find . -name "*.sln" -maxdepth 2 -exec dotnet restore {} \;

# Publish dự án Presentation
RUN dotnet publish "src/AICustomerSupport.Presentation/AICustomerSupport.Presentation.csproj" -c Release -o /app/publish

# Stage 2: Runtime (Nâng lên 10.0)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5145
ENTRYPOINT ["dotnet", "AICustomerSupport.Presentation.dll"]