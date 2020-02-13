FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
 ca-certificates \
 libc6 \
 libgcc1 \
 libgssapi-krb5-2 \
 liblttng-ust0 \
 libstdc++6 \
 zlib1g \
 libgdiplus \
 libc6-dev \
 curl \
 && rm -rf /var/lib/apt/lists/* \
 && ln -s /usr/lib/libgdiplus.so /usr/lib/gdiplus.dll

EXPOSE 80
EXPOSE 443

FROM base AS final
WORKDIR /app
COPY . .

ENTRYPOINT ["dotnet", "SerEngineRest.dll"]