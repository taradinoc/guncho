# Multi-stage Dockerfile for Guncho.WebHost
# This builds the modern .NET 10 implementation with Blazor WASM client

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution structure
COPY src/Directory.Build.props src/
COPY src/Guncho.slnx src/

# Copy TextfyreVM dependency (required for Guncho.Engine)
COPY TextfyreVM.dll ./

# Copy project files for dependency restoration
COPY src/Guncho.Engine/Guncho.Engine.csproj src/Guncho.Engine/
COPY src/Guncho.Shared/Guncho.Shared.csproj src/Guncho.Shared/
COPY src/Guncho.Client/Guncho.Client.csproj src/Guncho.Client/
COPY src/Guncho.WebHost/Guncho.WebHost.csproj src/Guncho.WebHost/

# Restore dependencies for WebHost (will also restore client as project reference)
WORKDIR /src/src/Guncho.WebHost
RUN dotnet restore

# Copy all source code
WORKDIR /src
COPY src/ src/

# Remove WebHost wwwroot to avoid conflicts (will be generated from Blazor client)
RUN rm -rf /src/src/Guncho.WebHost/wwwroot

# Build and publish the WebHost (will also publish Blazor client via build target)
WORKDIR /src/src/Guncho.WebHost
RUN dotnet publish -c Release -o /app/publish /p:RunAOTCompilation=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install dependencies for Inform 7 compiler (if needed at runtime)
# The compiler requires some Linux tools
RUN apt-get update && apt-get install -y \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Workaround for .NET 10 HotReload file naming bug
# The published file has a double hash but the loader expects a single hash
RUN if [ -d /app/wwwroot/_content/Microsoft.DotNet.HotReload.WebAssembly.Browser ]; then \
    cd /app/wwwroot/_content/Microsoft.DotNet.HotReload.WebAssembly.Browser/ && \
    for file in *.*.lib.module.js*; do \
        # Extract the hash from the filename (first occurrence)
        hash=$(echo "$file" | grep -oP '\.[a-z0-9]+\.' | head -1 | tr -d '.') && \
        # Generate the target filename with single hash
        newname=$(echo "$file" | sed "s/\.$hash\.$hash\.lib\.module\.js/.$hash.lib.module.js/") && \
        if [ "$file" != "$newname" ] && [ ! -e "$newname" ]; then \
            ln -s "$file" "$newname"; \
        fi; \
    done; \
fi

# Copy runtime data and resources from the workspace
# These are needed for realm compilation and game execution
COPY RealmData/ /app/RealmData/
COPY Skeleton.inform/ /app/Skeleton.inform/
COPY HackedI7/ /app/HackedI7/

# Create necessary directories
RUN mkdir -p /app/Cache /app/Logs

# Set environment variables for containerized paths
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_MODIFIABLE_ASSEMBLIES=
ENV Guncho__CachePath=/app/Cache
ENV Guncho__RealmDataPath=/app/RealmData
ENV Guncho__LogPath=/app/Logs
ENV Guncho__NiInstallationsPath=/app/HackedI7
ENV Guncho__NiSkeletonPath=/app/Skeleton.inform
ENV Guncho__StartRealmName="The Outer Realm"
ENV Guncho__WebServerPort=5000
ENV Guncho__GameServerPort=4108

# Expose ports:
# - 5000: Web server (ASP.NET Core Kestrel with Blazor UI)
# - 4108: TCP server (telnet/MUD protocol for game connections)
EXPOSE 5000 4108

# Run the application
ENTRYPOINT ["dotnet", "Guncho.WebHost.dll"]
