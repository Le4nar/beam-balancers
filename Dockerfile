FROM debian:bookworm-slim AS base

RUN apt-get update && apt-get install -y --no-install-recommends \
    libc6 libgcc-s1 libgssapi-krb5-2 libssl3 libstdc++6 zlib1g libicu72 \
    chromium chromium-common \
    libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 libdrm2 libxkbcommon0 \
    libxcomposite1 libxdamage1 libxrandr2 libgbm1 libpango-1.0-0 libcairo2 \
    fonts-liberation ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore \
       --install-dir /usr/share/dotnet --no-path \
    && ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

WORKDIR /app
COPY . .
RUN mkdir -p cache/fdb cache/mirage logs wwwroot

ENV CHROME_PATH=/usr/bin/chromium \
    DOTNET_ROOT=/usr/share/dotnet \
    ASPNETCORE_URLS=http://+:9118

EXPOSE 9118
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s \
    CMD curl -f http://localhost:9118/ || exit 1

ENTRYPOINT ["dotnet", "Core.dll"]
