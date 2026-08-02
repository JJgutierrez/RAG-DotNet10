#!/usr/bin/env bash
set -e

echo "Stopping local infrastructure..."
docker compose down -v
echo "Infrastructure stopped."
