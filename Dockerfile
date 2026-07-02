# # FROM docker-registry-002.zeuslearning.com/zeuslearning/dotnet/sdk:10.0-alpine AS build
# # WORKDIR /src
# # COPY . .
# # # RUN dotnet restore
# # RUN dotnet publish -c Release -o /app

# # FROM docker-registry-002.zeuslearning.com/zeuslearning/dotnet/aspnet:10.0-alpine
# # WORKDIR /app
# # COPY --from=build /app .
# # ENTRYPOINT ["dotnet", "TraineeManagement.api.dll"]

# # Stage 1: Build
# FROM docker-registry-002.zeuslearning.com/zeuslearning/dotnet/sdk:10.0-alpine AS build
# WORKDIR /src

# # Copy csproj and restore dependencies
# COPY *.csproj ./
# RUN dotnet restore

# # Copy the rest of the source and build
# COPY . ./
# RUN dotnet publish -c Release -o /app/publish

# # Stage 2: Runtime
# FROM docker-registry-002.zeuslearning.com/zeuslearning/dotnet/aspnet:10.0-alpine AS runtime
# WORKDIR /app
# COPY --from=build /app/publish .
# ENTRYPOINT ["dotnet", "TraineeManagement.api.dll"]

FROM docker-registry-002.zeuslearning.com/zeuslearning/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY ./publish .
ENTRYPOINT ["dotnet", "SubmissionProcessor.Worker.dll"]