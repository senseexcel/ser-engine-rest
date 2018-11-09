# Build docker image
FROM debian:stretch-slim

WORKDIR /usr/src/app
COPY . .
EXPOSE 80

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        libc6 \
        libgcc1 \
        libgssapi-krb5-2 \
        libicu57 \
        liblttng-ust0 \
        libssl1.0.2 \
        libstdc++6 \
        zlib1g \
		libgdiplus \
		libc6-dev \
		curl \
    && rm -rf /var/lib/apt/lists/*

RUN ln -s /usr/lib/libgdiplus.so /usr/lib/gdiplus.dll

ENV DOTNET_VERSION 2.1.0

RUN curl -SL --output dotnet.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/Runtime/$DOTNET_VERSION/dotnet-runtime-$DOTNET_VERSION-linux-x64.tar.gz \
    && dotnet_sha512='f93edfc068290347df57fd7b0221d0d9f9c1717257ed3b3a7b4cc6cc3d779d904194854e13eb924c30eaf7a8cc0bd38263c09178bc4d3e16281f552a45511234' \
    && echo "$dotnet_sha512 dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -zxf dotnet.tar.gz -C /usr/share/dotnet \
    && rm dotnet.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "ser-engine-rest.dll"]