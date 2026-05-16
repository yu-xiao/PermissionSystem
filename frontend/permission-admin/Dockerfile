FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL=
ARG VITE_OAUTH_CLIENT_ID=permission-admin
ARG VITE_OAUTH_CLIENT_SECRET=permission-admin-secret

ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_OAUTH_CLIENT_ID=$VITE_OAUTH_CLIENT_ID
ENV VITE_OAUTH_CLIENT_SECRET=$VITE_OAUTH_CLIENT_SECRET

COPY frontend/permission-admin/package*.json ./
RUN npm ci

COPY frontend/permission-admin/ ./
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY frontend/permission-admin/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
