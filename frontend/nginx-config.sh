#!/bin/sh
# Runs on container start (nginx image executes /docker-entrypoint.d/*.sh).
# Writes config.js from the API_URL environment variable.
set -e
API_URL="${API_URL:-http://localhost:8080}"
cat > /usr/share/nginx/html/config.js <<CONF
window.__CONFIG__ = { API_URL: "${API_URL}" };
CONF
