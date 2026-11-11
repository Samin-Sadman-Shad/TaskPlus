# Smart Task web client: build the Angular app, then serve it with nginx (non-root).
# nginx also forwards /api to the API container, so the browser talks to a single origin and the
# SameSite=Strict refresh cookie works exactly as with the dev-server proxy.

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /app

# Dependencies first, so source changes don't invalidate the npm layer.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npx ng build --configuration production

# ---- serve ----
FROM nginxinc/nginx-unprivileged:1.29-alpine AS final

COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/smart-task-web/browser /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
