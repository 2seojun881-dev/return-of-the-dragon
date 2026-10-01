FROM node:22-slim
WORKDIR /app
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund
COPY broker.js idle.js ./
ENV PORT=8090 DATA=/data/idle.json
EXPOSE 8090
CMD ["node", "idle.js"]
