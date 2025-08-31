FROM mcr.microsoft.com/vscode/devcontainers/dotnet:7.0-bookworm-slim

ENV WORKDIR="/app" \
    USER="vscode"

WORKDIR $WORKDIR

COPY --chown=${USER}:${USER} . ${WORKDIR}

COPY --chown=${USERNAME}:${USERNAME} docker-entrypoint.sh /usr/bin/
RUN chmod +x /usr/bin/docker-entrypoint.sh

ENTRYPOINT ["/usr/bin/docker-entrypoint.sh"]

RUN dotnet tool restore && \
    dotnet paket restore && \
    dotnet restore