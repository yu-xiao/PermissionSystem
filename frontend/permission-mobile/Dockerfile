# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
ARG VITE_API_BASE_URL
ARG VITE_APP_VERSION
ARG VITE_OAUTH_ISSUER
ARG VITE_OAUTH_CLIENT_ID
ARG VITE_OAUTH_REDIRECT_URI
ARG VITE_OAUTH_SCOPE
ARG VITE_DEFAULT_TENANT_CODE
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_APP_VERSION=$VITE_APP_VERSION \
    VITE_OAUTH_ISSUER=$VITE_OAUTH_ISSUER \
    VITE_OAUTH_CLIENT_ID=$VITE_OAUTH_CLIENT_ID \
    VITE_OAUTH_REDIRECT_URI=$VITE_OAUTH_REDIRECT_URI \
    VITE_OAUTH_SCOPE=$VITE_OAUTH_SCOPE \
    VITE_DEFAULT_TENANT_CODE=$VITE_DEFAULT_TENANT_CODE
RUN npm run build

FROM nginx:1.27-alpine AS runtime
ENV API_UPSTREAM=http://permission-system-api:8080 \
    NGINX_ENVSUBST_TEMPLATE_VARS=API_UPSTREAM
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s CMD wget -q -O - http://127.0.0.1/healthz || exit 1
