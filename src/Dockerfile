# Build docker image
FROM microsoft/dotnet:2.1-sdk

WORKDIR /usr/src/app
COPY . .
EXPOSE 11271

RUN apt-get update
RUN apt-get install -y libgdiplus
RUN apt-get install -y libc6-dev
RUN ln -s /usr/lib/libgdiplus.so /usr/lib/gdiplus.dll

ENTRYPOINT ["dotnet", "ser-service.dll"]