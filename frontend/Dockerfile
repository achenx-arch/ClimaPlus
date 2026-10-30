# syntax=docker/dockerfile:1
# =====================================================================
#  ClimaPlus – Imagen del frontend (React + Vite servido con Nginx) · Fase 9
#  VITE_API_URL se fija al compilar: es la URL pública de la API que verá el navegador.
#  Construir:  docker build --build-arg VITE_API_URL=http://localhost:8080 -t climaplus-web ./frontend
# =====================================================================

FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=${VITE_API_URL}
RUN npm run build

# Nginx sin privilegios (escucha en 8080 y corre con el usuario nginx).
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/cabeceras-seguridad.conf /etc/nginx/snippets/cabeceras-seguridad.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --retries=3 CMD wget -q --spider http://127.0.0.1:8080/ || exit 1
