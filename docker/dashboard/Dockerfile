FROM node:24-alpine AS build
WORKDIR /src/dashboard
COPY dashboard/package*.json ./
RUN npm ci
COPY dashboard .
RUN npm run build

FROM nginx:alpine
COPY --from=build /src/dashboard/dist /usr/share/nginx/html
COPY docker/dashboard/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
