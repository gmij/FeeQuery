#!/bin/bash
# FeeQuery build & run script (Docker Compose)
# Requires: Docker and Docker Compose

set -e

echo "========================================"
echo "  FeeQuery - Docker Compose Build"
echo "========================================"
docker-compose up -d --build

echo ""
echo "========================================"
echo "  Started successfully!"
echo "========================================"
echo "  URL   : http://localhost:8080"
echo ""
echo "  Logs  : docker-compose logs -f"
echo "  Stop  : docker-compose down"
echo "  Clean : docker-compose down -v"
echo "========================================"
