# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app

# tzdata provides the IANA time zones (e.g. America/Argentina/Buenos_Aires) for the server time zone setting.
RUN apk add --no-cache icu-libs tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080
EXPOSE 45901
EXPOSE 45902
EXPOSE 45903
EXPOSE 45904
EXPOSE 45905
EXPOSE 45906
EXPOSE 44405
EXPOSE 45980

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
RUN apk add --no-cache rsync

# The build runs in a cache mount which survives between image builds, like a local working copy:
# rsync only updates the changed files (keeping the timestamps), and the bin/obj folders from
# the previous build stay, so MSBuild recompiles only the projects which actually changed.
# The NuGet packages are cached, too. The analyzers (StyleCop etc.) only matter in the IDE.
RUN --mount=type=bind,target=/context \
    --mount=type=cache,id=openmu-build,target=/build \
    --mount=type=cache,id=openmu-nuget,target=/root/.nuget/packages \
    rsync -a --delete --exclude bin/ --exclude obj/ /context/ /build/src/ \
    && cd /build/src \
    && dotnet publish "Startup/MUnique.OpenMU.Startup.csproj" \
        -c Release \
        -o /app/publish \
        -p:ci=true \
        -p:RunAnalyzers=false

FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

# The container runs as a non-root user, but /app belongs to root. Directories the
# application writes to have to be created and made writable beforehand - the data
# protection key ring among them, which protects the admin panel sessions.
RUN mkdir -p /app/logs /app/data-protection-keys && chmod 777 /app/logs /app/data-protection-keys

ARG APP_UID=1000
USER ${APP_UID}

ENTRYPOINT ["dotnet", "MUnique.OpenMU.Startup.dll", "-autostart"]