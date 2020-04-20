FROM node:13 as base

FROM base as builder

RUN mkdir /install
WORKDIR /install

COPY samples/clients/node/package.json package.json

RUN npm install

FROM base

COPY --from=builder /install/node_modules /app/node_modules
COPY samples/clients/node/*.js /app
COPY /protos/appencryption.proto /app/protos/appencryption.proto

WORKDIR /app
RUN ls protos


ENTRYPOINT ["node", "appencryption_client.js"]
CMD ["--help"]
