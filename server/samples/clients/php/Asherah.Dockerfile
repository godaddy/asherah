ARG ALPINEVERSION=3.11
# NOTE: the normal Asherah Server image should already be built!
FROM asherah_sidecar_build:latest AS sidecar
# Start from a new image
FROM alpine:${ALPINEVERSION}
# Copy over the Asherah Server binary
COPY --from=sidecar /asherah-server /app/asherah-server
# UID/GID
ARG ASHERAH_UID=1982
ARG ASHERAH_GID=1982
# Set up container
RUN apk add libcap \
 && addgroup -S -g ${ASHERAH_GID} asherah \
 && adduser -S -G asherah -H -D -s /bin/false -g "Docker image user" -u ${ASHERAH_UID} asherah \
 && mkdir /sock \
 && chown -R asherah:asherah /sock \
 && chown -R asherah:asherah /app \
 && setcap cap_ipc_lock=+ep /app/asherah-server
# run as asherah
USER asherah
# re-map the entrypoint and cmd
ENTRYPOINT ["/app/asherah-server"]
CMD ["--help"]
