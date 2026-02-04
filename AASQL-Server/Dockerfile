FROM alpine:latest AS fetcher
WORKDIR /install

RUN apk add --no-cache curl unzip

ENV AAS_VERSION="v0.3.1.552-aasV3-alpha-latest"
ENV AAS_ZIP="AasxServerBlazor.${AAS_VERSION}.zip"

RUN curl -L -o ${AAS_ZIP} https://github.com/eclipse-aaspe/server/releases/download/${AAS_VERSION}/${AAS_ZIP} \
    && unzip ${AAS_ZIP} -d /temp_extract \
    && mkdir -p /app \
    && find /temp_extract -maxdepth 2 -name "*.dll" -exec cp -r /temp_extract/{} /app/ \; \
    && cp -r /temp_extract/*/* /app/ 2>/dev/null || cp -r /temp_extract/* /app/ \
    && rm -rf /temp_extract ${AAS_ZIP}


FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app

EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001

COPY --from=fetcher /app .

RUN mkdir -p ./aasxs

ENTRYPOINT ["dotnet"]
CMD ["AasxServerBlazor.dll", "--no-security", "--data-path", "./aasxs", "--external-blazor", "http://localhost:5001", "--with-db"]