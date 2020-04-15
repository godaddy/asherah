FROM node:13-alpine as base

RUN mkdir /app
WORKDIR /app

COPY samples/clients/node/package.json package.json
COPY samples/clients/node/*.js .
COPY /protos/appencryption.proto protos/appencryption.proto

RUN npm install
RUN ls

ENTRYPOINT ["node", "appencryption_client.js"]
CMD ["--help"]
